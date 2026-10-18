
using SecurePaymentApi.Api.Models;

namespace SecurePaymentApi.Api.DTOs;

public sealed record PaymentResponse(
    Guid Id,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    PaymentStatus Status,
    DateTime CreatedAtUtc);
