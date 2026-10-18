
namespace SecurePaymentApi.Api.Models;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string AccountHolderId { get; set; }

    public decimal Balance { get; set; }

    // Used to detect conflicting concurrent updates.
    public Guid Version { get; set; } = Guid.NewGuid();
}
