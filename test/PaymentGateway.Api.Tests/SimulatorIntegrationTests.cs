using System.Net;
using System.Net.Http.Json;
using System.Text;

using Microsoft.AspNetCore.Mvc.Testing;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Tests.Support;

namespace PaymentGateway.Api.Tests;

/// <summary>
/// End to end against the real bank simulator (<c>docker-compose up</c>).
/// Skipped when nothing listens on BANK_SIMULATOR_URL, so the suite stays green without Docker.
/// </summary>
public class SimulatorIntegrationTests
{
    private static class Simulator
    {
        public static readonly string Url = Environment.GetEnvironmentVariable("BANK_SIMULATOR_URL") ?? "http://localhost:8080";
        public static readonly bool IsUp = Probe();

        private static bool Probe()
        {
            try
            {
                using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(1) };
                client.PostAsync($"{Url}/payments", new StringContent("{}", Encoding.UTF8, "application/json")).GetAwaiter().GetResult();
                return true;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                return false;
            }
        }
    }

    private sealed class SimulatorTheoryAttribute : TheoryAttribute
    {
        public SimulatorTheoryAttribute()
        {
            if (!Simulator.IsUp)
            {
                Skip = $"no bank simulator at {Simulator.Url}";
            }
        }
    }

    [SimulatorTheory]
    [InlineData(Payments.AuthorizedCard, HttpStatusCode.Created, PaymentStatus.Authorized)]
    [InlineData(Payments.DeclinedCard, HttpStatusCode.Created, PaymentStatus.Declined)]
    [InlineData(Payments.BankDownCard, HttpStatusCode.BadGateway, null)]
    public async Task RoundTripThroughTheSimulator(string cardNumber, HttpStatusCode expectedStatus, PaymentStatus? expectedResult)
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Bank:BaseUrl", Simulator.Url));
        HttpClient client = factory.CreateClient();
        Dictionary<string, object?> request = Payments.Valid();
        request["cardNumber"] = cardNumber;

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/payments", request);

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedResult is { } status)
        {
            Payment payment = (await response.Content.ReadFromJsonAsync<Payment>(Json.Options))!;
            Assert.Equal(status, payment.Status);
            HttpResponseMessage fetched = await client.GetAsync($"/api/payments/{payment.Id}");
            Assert.Equal(payment, await fetched.Content.ReadFromJsonAsync<Payment>(Json.Options));
        }
    }
}