
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SecurePaymentApi.Api.DTOs;
using SecurePaymentApi.Api.Services;
using SecurePaymentApi.Api.Models;

namespace SecurePaymentApi.Tests;

public class PaymentEndpointTests
{
    [Fact]
    public async Task CreatePayment_WhenConcurrencyConflictOccurs_Returns409()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.UseSetting(
                    "ConnectionStrings:PaymentDatabase",
                    "Host=localhost;Port=5432;Database=testdb;Username=testuser;Password=testpassword");

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IPaymentService>();
                    services.AddScoped<IPaymentService, ConcurrencyConflictPaymentService>();
                });
            });

        using var client = factory.CreateClient();

        var request = new CreatePaymentRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "test-idempotency-key");

        var response = await client.PostAsJsonAsync("/payments", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }


    [Fact]
    public async Task CreatePayment_WithSameRequestTwice_ReturnsCreatedBothTimes()
    {
        var fakeService = new FakePaymentService();

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting(
                    "ConnectionStrings:PaymentDatabase",
                    "Host=localhost;Port=5432;Database=testdb;Username=testuser;Password=testpassword");

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IPaymentService>();
                    services.AddSingleton<IPaymentService>(fakeService);
                });
            });

        using var client = factory.CreateClient();

        var request = new CreatePaymentRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            "retry-test-123");

        var firstResponse = await client.PostAsJsonAsync("/payments", request);
        var secondResponse = await client.PostAsJsonAsync("/payments", request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal(1, fakeService.CallCount);
    }



    private sealed class FakePaymentService : IPaymentService
    {
        private readonly Dictionary<string, PaymentResponse> _payments = new();

        public int CallCount { get; private set; }

        public Task<PaymentResponse> CreatePaymentAsync(
            CreatePaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_payments.TryGetValue(request.IdempotencyKey, out var existingPayment))
            {
                if (existingPayment.FromAccountId != request.FromAccountId ||
                    existingPayment.ToAccountId != request.ToAccountId ||
                    existingPayment.Amount != request.Amount)
                {
                    throw new ArgumentException(
                        "This idempotency key has already been used for a different payment.");
                }

                return Task.FromResult(existingPayment);
            }

            CallCount++;

            var payment = new PaymentResponse(
                Guid.NewGuid(),
                request.FromAccountId,
                request.ToAccountId,
                request.Amount,
                PaymentStatus.Completed,
                DateTime.UtcNow);

            _payments[request.IdempotencyKey] = payment;

            return Task.FromResult(payment);
        }
    }


    private sealed class ConcurrencyConflictPaymentService : IPaymentService
    {
        public Task<PaymentResponse> CreatePaymentAsync(
            CreatePaymentRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new PaymentConcurrencyException(
                "The account balance changed during this payment. Please refresh and try again.",
                new Exception("Simulated concurrency conflict"));
        }
    }


}
