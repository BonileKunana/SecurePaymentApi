
namespace SecurePaymentApi.Api.Services;

public sealed class PaymentConcurrencyException : Exception
{
    public PaymentConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

