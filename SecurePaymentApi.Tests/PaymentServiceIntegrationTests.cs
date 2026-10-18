
using Microsoft.EntityFrameworkCore;
using SecurePaymentApi.Api.Data;
using SecurePaymentApi.Api.DTOs;
using SecurePaymentApi.Api.Models;
using SecurePaymentApi.Api.Services;
using Xunit;

namespace SecurePaymentApi.Tests;

public class PaymentServiceIntegrationTests
{
    [Fact]
    public async Task CreatePayment_ValidPayment_UpdatesBalancesAndSavesPayment()
    {
        // Read the existing PostgreSQL password from the environment.
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

        Assert.False(
            string.IsNullOrWhiteSpace(password),
            "Set the POSTGRES_PASSWORD environment variable before running this test.");

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(
                $"Host=localhost;Port=5433;Database=securepayment_test;" +
                $"Username=paymentuser;Password={password}")
            .Options;

        // Apply the existing EF Core migrations to the test database.
        await using var db = new PaymentDbContext(options);
        await db.Database.MigrateAsync();

        // Clear only test data, preserving the migration history.
        await db.Database.ExecuteSqlRawAsync("""DELETE FROM "Payments";""");
        await db.Database.ExecuteSqlRawAsync("""DELETE FROM "Accounts";""");

        var sender = new Account
        {
            AccountHolderId = "integration-sender",
            Balance = 500m
        };

        var recipient = new Account
        {
            AccountHolderId = "integration-recipient",
            Balance = 100m
        };

        db.Accounts.AddRange(sender, recipient);
        await db.SaveChangesAsync();

        var service = new PaymentService(db);

        var request = new CreatePaymentRequest(
            sender.Id,
            recipient.Id,
            125m,
            $"integration-{Guid.NewGuid():N}");

        // Act: execute the real service against PostgreSQL.
        var result = await service.CreatePaymentAsync(request);

        // Reload balances from the database.
        await db.Entry(sender).ReloadAsync();
        await db.Entry(recipient).ReloadAsync();

        Assert.Equal(375m, sender.Balance);
        Assert.Equal(225m, recipient.Balance);
        Assert.Equal(125m, result.Amount);
        Assert.Equal(PaymentStatus.Completed, result.Status);
        Assert.Equal(1, await db.Payments.CountAsync());
    }


    [Fact]
    public async Task CreatePayment_SameRequestTwice_TransfersMoneyOnlyOnce()
    {
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

        Assert.False(string.IsNullOrWhiteSpace(password));

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(
                $"Host=localhost;Port=5433;Database=securepayment_test;" +
                $"Username=paymentuser;Password={password}")
            .Options;

        await using var db = new PaymentDbContext(options);
        await db.Database.MigrateAsync();

        await db.Database.ExecuteSqlRawAsync("""DELETE FROM "Payments";""");
        await db.Database.ExecuteSqlRawAsync("""DELETE FROM "Accounts";""");

        var sender = new Account
        {
            AccountHolderId = "idempotency-sender",
            Balance = 500m
        };

        var recipient = new Account
        {
            AccountHolderId = "idempotency-recipient",
            Balance = 100m
        };

        db.Accounts.AddRange(sender, recipient);
        await db.SaveChangesAsync();

        var service = new PaymentService(db);

        var request = new CreatePaymentRequest(
            sender.Id,
            recipient.Id,
            125m,
            $"idempotency-{Guid.NewGuid():N}");

        var firstResult = await service.CreatePaymentAsync(request);
        var secondResult = await service.CreatePaymentAsync(request);

        await db.Entry(sender).ReloadAsync();
        await db.Entry(recipient).ReloadAsync();

        Assert.Equal(firstResult.Id, secondResult.Id);
        Assert.Equal(375m, sender.Balance);
        Assert.Equal(225m, recipient.Balance);
        Assert.Equal(1, await db.Payments.CountAsync());
    }


    [Fact]
    public async Task CreatePayment_InsufficientFunds_DoesNotChangeBalancesOrSavePayment()
    {
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

        Assert.False(string.IsNullOrWhiteSpace(password));

        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseNpgsql(
                $"Host=localhost;Port=5433;Database=securepayment_test;" +
                $"Username=paymentuser;Password={password}")
            .Options;

        await using var db = new PaymentDbContext(options);
        await db.Database.MigrateAsync();

        await db.Database.ExecuteSqlRawAsync("""DELETE FROM "Payments";""");
        await db.Database.ExecuteSqlRawAsync("""DELETE FROM "Accounts";""");

        var sender = new Account
        {
            AccountHolderId = "insufficient-funds-sender",
            Balance = 50m
        };

        var recipient = new Account
        {
            AccountHolderId = "insufficient-funds-recipient",
            Balance = 100m
        };

        db.Accounts.AddRange(sender, recipient);
        await db.SaveChangesAsync();

        var service = new PaymentService(db);

        var request = new CreatePaymentRequest(
            sender.Id,
            recipient.Id,
            125m,
            $"insufficient-{Guid.NewGuid():N}");

        // The sender has R50 but attempts to send R125.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreatePaymentAsync(request));

        // Verify persisted balances and payment records.
        await db.Entry(sender).ReloadAsync();
        await db.Entry(recipient).ReloadAsync();

        Assert.Equal(50m, sender.Balance);
        Assert.Equal(100m, recipient.Balance);
        Assert.Equal(0, await db.Payments.CountAsync());
    }


}
