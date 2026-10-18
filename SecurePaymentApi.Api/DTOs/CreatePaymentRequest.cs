namespace SecurePaymentApi.Api.DTOs;

public sealed record CreatePaymentRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string IdempotencyKey);