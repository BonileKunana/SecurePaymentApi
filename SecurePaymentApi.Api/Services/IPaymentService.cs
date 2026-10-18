using SecurePaymentApi.Api.DTOs;

namespace SecurePaymentApi.Api.Services;

public interface IPaymentService
{
    Task<PaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default);
}