using Microsoft.EntityFrameworkCore;
using SecurePaymentApi.Api.Data;
using SecurePaymentApi.Api.DTOs;
using SecurePaymentApi.Api.Models;
using SecurePaymentApi.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("PaymentDatabase")
    ?? throw new InvalidOperationException(
        "PaymentDatabase connection string is not configured.");

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<IPaymentService>(
    services => services.GetRequiredService<PaymentService>());



// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");


app.MapPost("/payments", async (
    CreatePaymentRequest request,
    IPaymentService paymentService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var payment = await paymentService.CreatePaymentAsync(
            request, cancellationToken);

        return Results.Created($"/payments/{payment.Id}", payment);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }

    catch (PaymentConcurrencyException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }

    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }

});

if (app.Environment.IsDevelopment())
{
    // create account
    app.MapPost("/dev/accounts", async (
        string accountHolderId,
        decimal initialBalance,
        PaymentDbContext dbContext,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(accountHolderId) ||
            initialBalance < 0 ||
            decimal.Round(initialBalance, 2) != initialBalance)
        {
            return Results.BadRequest(new
            {
                error = "Provide an account holder ID and a non-negative balance with at most two decimal places."
            });
        }

        var account = new Account
        {
            AccountHolderId = accountHolderId,
            Balance = initialBalance
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/dev/accounts/{account.Id}",
            new { account.Id, account.AccountHolderId, account.Balance });
    });

    // get all accounts
    app.MapGet("/dev/accounts", async (
    PaymentDbContext dbContext,
    CancellationToken cancellationToken) =>
    {
        var accounts = await dbContext.Accounts
            .AsNoTracking()
            .OrderBy(a => a.AccountHolderId)
            .Select(a => new
            {
                a.Id,
                a.AccountHolderId,
                a.Balance
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(accounts);
    });


    // get payment history
    app.MapGet("/dev/payments", async (
    PaymentDbContext dbContext,
    CancellationToken cancellationToken) =>
    {
        var payments = await dbContext.Payments
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new
            {
                p.Id,
                p.FromAccountId,
                p.ToAccountId,
                p.Amount,
                p.Status,
                p.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(payments);
    });
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public partial class Program { }