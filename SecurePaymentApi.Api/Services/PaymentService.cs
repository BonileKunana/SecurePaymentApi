
using Microsoft.EntityFrameworkCore;
using SecurePaymentApi.Api.Data;
using SecurePaymentApi.Api.DTOs;
using SecurePaymentApi.Api.Models;
using Npgsql;

namespace SecurePaymentApi.Api.Services;

public class PaymentService(PaymentDbContext dbContext) : IPaymentService
{
    public async Task<PaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate the idempotency key.
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            request.IdempotencyKey.Length > 100)
        {
            throw new ArgumentException(
                "Idempotency key is required and must not exceed 100 characters.");
        }

        // Validate the payment details.
        if (request.FromAccountId == request.ToAccountId)
        {
            throw new ArgumentException(
                "Sender and recipient must be different accounts.");
        }

        if (request.Amount <= 0 ||
            decimal.Round(request.Amount, 2) != request.Amount)
        {
            throw new ArgumentException(
                "Amount must be positive and have no more than two decimal places.");
        }

        // Check whether this key was already used.
        var existingPayment = await dbContext.Payments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.IdempotencyKey == request.IdempotencyKey,
                cancellationToken);

        if (existingPayment is not null)
        {
            // A key cannot be reused for different payment details.
            if (existingPayment.FromAccountId != request.FromAccountId ||
                existingPayment.ToAccountId != request.ToAccountId ||
                existingPayment.Amount != request.Amount)
            {
                throw new ArgumentException(
                    "This idempotency key has already been used for a different payment.");
            }

            // Return the original payment without transferring money again.
            return new PaymentResponse(
                existingPayment.Id,
                existingPayment.FromAccountId,
                existingPayment.ToAccountId,
                existingPayment.Amount,
                existingPayment.Status,
                existingPayment.CreatedAtUtc);
        }

        // Keep balance changes and the payment record in one transaction.
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var sender = await dbContext.Accounts
            .SingleOrDefaultAsync(
                a => a.Id == request.FromAccountId,
                cancellationToken);

        var recipient = await dbContext.Accounts
            .SingleOrDefaultAsync(
                a => a.Id == request.ToAccountId,
                cancellationToken);

        if (sender is null || recipient is null)
        {
            throw new KeyNotFoundException(
                "Sender or recipient account was not found.");
        }

        if (sender.Balance < request.Amount)
        {
            throw new InvalidOperationException("Insufficient funds.");
        }

        // Update both account balances and concurrency versions.
        sender.Balance -= request.Amount;
        sender.Version = Guid.NewGuid();

        recipient.Balance += request.Amount;
        recipient.Version = Guid.NewGuid();

        // Create the payment record.
        var payment = new Payment
        {
            IdempotencyKey = request.IdempotencyKey,
            FromAccountId = sender.Id,
            ToAccountId = recipient.Id,
            Amount = request.Amount,
            Status = PaymentStatus.Completed
        };

        dbContext.Payments.Add(payment);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException postgresException &&
                  postgresException.SqlState == "23505")
        {
            await transaction.RollbackAsync(cancellationToken);

            // Clear tracked entities before querying again.
            dbContext.ChangeTracker.Clear();

            // Look for the payment that won the race.
            var duplicatePayment = await dbContext.Payments
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    p => p.IdempotencyKey == request.IdempotencyKey,
                    cancellationToken);

            if (duplicatePayment is not null &&
                duplicatePayment.FromAccountId == request.FromAccountId &&
                duplicatePayment.ToAccountId == request.ToAccountId &&
                duplicatePayment.Amount == request.Amount)
            {
                return new PaymentResponse(
                    duplicatePayment.Id,
                    duplicatePayment.FromAccountId,
                    duplicatePayment.ToAccountId,
                    duplicatePayment.Amount,
                    duplicatePayment.Status,
                    duplicatePayment.CreatedAtUtc);
            }

            throw new InvalidOperationException(
                "The idempotency key has already been used or a database constraint was violated.",
                ex);
        }

        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            throw new PaymentConcurrencyException(
                "The account balance changed during this payment. Please refresh and try again.",
                ex);
        }


        return new PaymentResponse(
            payment.Id,
            payment.FromAccountId,
            payment.ToAccountId,
            payment.Amount,
            payment.Status,
            payment.CreatedAtUtc);
    }
}
