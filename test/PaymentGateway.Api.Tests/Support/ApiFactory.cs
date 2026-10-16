using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Support;

/// <summary>The real application with the bank swapped for <see cref="FakeBank"/>.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public FakeBank Bank { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAcquiringBank>();
            services.AddSingleton<IAcquiringBank>(Bank);
        });
    }
}

public static class Payments
{
    public const string AuthorizedCard = "2222405343248877"; // odd last digit
    public const string DeclinedCard = "2222405343248878"; // even last digit
    public const string BankDownCard = "2222405343248870"; // last digit 0

    /// <summary>A request that passes every rule; override a field to break one.</summary>
    public static Dictionary<string, object?> Valid()
    {
        return new Dictionary<string, object?>
        {
            ["cardNumber"] = AuthorizedCard,
            ["expiryMonth"] = 12,
            ["expiryYear"] = DateTime.UtcNow.Year + 1,
            ["currency"] = "GBP",
            ["amount"] = 1050,
            ["cvv"] = "123"
        };
    }
}