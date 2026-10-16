using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Tests.Support;

namespace PaymentGateway.Api.Tests;

/// <summary>
/// Every rule of the brief, one case each. A rejected request must never reach
/// the bank and must never echo the card number or CVV back.
/// </summary>
public class ValidationTests : IDisposable
{
    private static readonly string Absent = "<absent>";

    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public ValidationTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    public static TheoryData<string, object?, string> Cases()
    {
        int lastYear = DateTime.UtcNow.Year - 1;
        return new TheoryData<string, object?, string>
        {
            { "cardNumber", Absent, "cardNumber" },
            { "cardNumber", "4242424242424", "cardNumber" }, // 13 digits
            { "cardNumber", new string('4', 20), "cardNumber" }, // 20 digits
            { "cardNumber", "4242 4242 4242 4242", "cardNumber" }, // non numeric
            { "expiryMonth", Absent, "expiryMonth" },
            { "expiryMonth", 0, "expiryMonth" },
            { "expiryMonth", 13, "expiryMonth" },
            { "expiryYear", Absent, "expiryYear" },
            { "expiryYear", lastYear, "expiryYear" }, // in the past
            { "currency", Absent, "currency" },
            { "currency", "GB", "currency" }, // 2 chars
            { "currency", "JPY", "currency" }, // not supported
            { "currency", "gbp", "currency" }, // lower case
            { "amount", Absent, "amount" },
            { "amount", 10.5, "amount" }, // not an integer
            { "amount", "1050", "amount" }, // a string
            { "amount", 0, "amount" },
            { "amount", -1, "amount" },
            { "cvv", Absent, "cvv" },
            { "cvv", "12", "cvv" }, // 2 digits
            { "cvv", "12345", "cvv" }, // 5 digits
            { "cvv", "12a", "cvv" } // non numeric
        };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task InvalidRequestIsRejectedWithoutCallingTheBank(string field, object? value, string expectedField)
    {
        Dictionary<string, object?> request = Payments.Valid();
        if (Equals(value, Absent))
        {
            request.Remove(field);
        }
        else
        {
            request[field] = value;
        }

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string text = await response.Content.ReadAsStringAsync();
        RejectedResponse rejected = JsonSerializer.Deserialize<RejectedResponse>(text, Json.Options)!;
        Assert.Equal(PaymentStatus.Rejected, rejected.Status);
        Assert.Equal(new[] { expectedField }, rejected.Errors.Select(e => e.Field).Distinct());
        Assert.Empty(_factory.Bank.Requests);
        // PCI: the rejection must not echo what was submitted.
        Assert.DoesNotContain(Payments.AuthorizedCard, text);
        Assert.DoesNotContain("\"123\"", text);
    }

    [Fact]
    public async Task CardExpiredLastMonthIsRejected()
    {
        DateTime lastMonth = DateTime.UtcNow.AddMonths(-1);
        Dictionary<string, object?> request = Payments.Valid();
        request["expiryMonth"] = lastMonth.Month;
        request["expiryYear"] = lastMonth.Year;

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("card has expired", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CardExpiringThisMonthIsStillValid()
    {
        DateTime today = DateTime.UtcNow;
        Dictionary<string, object?> request = Payments.Valid();
        request["expiryMonth"] = today.Month;
        request["expiryYear"] = today.Year;

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/payments", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task SeveralErrorsAreAllReported()
    {
        Dictionary<string, object?> request = Payments.Valid();
        request["cvv"] = "1";
        request["currency"] = "XXX";

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        RejectedResponse rejected = (await response.Content.ReadFromJsonAsync<RejectedResponse>(Json.Options))!;
        Assert.Equal(new[] { "currency", "cvv" }, rejected.Errors.Select(e => e.Field).OrderBy(f => f));
    }

    [Fact]
    public async Task MalformedJsonIsRejected()
    {
        HttpResponseMessage response = await _client.PostAsync(
            "/api/payments", new StringContent("{not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"status\":\"Rejected\"", await response.Content.ReadAsStringAsync());
    }
}