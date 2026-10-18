
namespace SecurePaymentApi.Api.Models;

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed
}

public class Payment
{
    public required string IdempotencyKey { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FromAccountId { get; set; }

    public Guid ToAccountId { get; set; }

    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
