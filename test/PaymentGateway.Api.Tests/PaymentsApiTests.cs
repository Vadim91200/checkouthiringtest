using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Tests.Support;

namespace PaymentGateway.Api.Tests;

public class PaymentsApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public PaymentsApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task AuthorizedPaymentIsCreatedAndMasked()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/payments", Payments.Valid());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        string text = await response.Content.ReadAsStringAsync();
        Payment payment = JsonSerializer.Deserialize<Payment>(text, Json.Options)!;
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal("8877", payment.CardNumberLastFour);
        Assert.Equal(12, payment.ExpiryMonth);
        Assert.Equal(DateTime.UtcNow.Year + 1, payment.ExpiryYear);
        Assert.Equal("GBP", payment.Currency);
        Assert.Equal(1050, payment.Amount);
        Assert.NotEqual(Guid.Empty, payment.Id);
        Assert.DoesNotContain(Payments.AuthorizedCard, text);
        Assert.Contains("\"status\":\"Authorized\"", text); // the enum is serialized by name
        Assert.Equal($"/api/Payments/{payment.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal(Payments.AuthorizedCard, Assert.Single(_factory.Bank.Requests).CardNumber);
    }

    [Fact]
    public async Task DeclinedPaymentIsCreatedToo()
    {
        Dictionary<string, object?> request = Payments.Valid();
        request["cardNumber"] = Payments.DeclinedCard;

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/payments", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(PaymentStatus.Declined, (await response.Content.ReadFromJsonAsync<Payment>(Json.Options))!.Status);
    }

    [Fact]
    public async Task BankUnavailableIsA502AndNothingIsStored()
    {
        Dictionary<string, object?> request = Payments.Valid();
        request["cardNumber"] = Payments.BankDownCard;

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/payments", request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        string text = await response.Content.ReadAsStringAsync();
        Assert.Contains("acquiring bank unavailable", text);
        Assert.DoesNotContain("\"id\"", text);
    }

    [Fact]
    public async Task EachPaymentGetsItsOwnId()
    {
        HashSet<Guid> ids = new();
        for (int i = 0; i < 3; i++)
        {
            HttpResponseMessage response = await _client.PostAsJsonAsync("/api/payments", Payments.Valid());
            ids.Add((await response.Content.ReadFromJsonAsync<Payment>(Json.Options))!.Id);
        }

        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public async Task AProcessedPaymentCanBeRetrieved()
    {
        Dictionary<string, object?> request = Payments.Valid();
        request["cardNumber"] = Payments.DeclinedCard;
        HttpResponseMessage created = await _client.PostAsJsonAsync("/api/payments", request);
        Payment payment = (await created.Content.ReadFromJsonAsync<Payment>(Json.Options))!;

        HttpResponseMessage response = await _client.GetAsync($"/api/payments/{payment.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(payment, await response.Content.ReadFromJsonAsync<Payment>(Json.Options));
        Assert.Equal(PaymentStatus.Declined, payment.Status);
    }

    [Fact]
    public async Task UnknownPaymentIs404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/payments/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/payments/not-a-guid")).StatusCode);
    }

    [Fact]
    public async Task ARejectedPaymentIsNeverStored()
    {
        Dictionary<string, object?> request = Payments.Valid();
        request["cvv"] = "12";

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("\"id\"", await response.Content.ReadAsStringAsync());
    }
}