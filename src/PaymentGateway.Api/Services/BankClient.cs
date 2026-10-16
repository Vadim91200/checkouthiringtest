using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

/// <summary>HTTP client for the acquiring bank (the Mountebank simulator in this repo).</summary>
public class BankClient : IAcquiringBank
{
    private static readonly JsonSerializerOptions SnakeCase = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _http;

    public BankClient(HttpClient http)
    {
        _http = http;
    }

    private record BankRequest(string CardNumber, string ExpiryDate, string Currency, int Amount, string Cvv);

    private record BankResponse(bool Authorized, string? AuthorizationCode);

    public async Task<bool> AuthorizeAsync(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        BankRequest body = new(
            request.CardNumber!,
            $"{request.ExpiryMonth!.Value:00}/{request.ExpiryYear}",
            request.Currency!,
            request.Amount!.Value,
            request.Cvv!);

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("payments", body, SnakeCase, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new BankUnavailableException($"bank unreachable: {exception.GetType().Name}", exception);
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            // 503 is the simulator's "bank down"; a 400 would mean our own
            // mapping is wrong. Either way there is no authorization decision.
            throw new BankUnavailableException($"bank answered HTTP {(int)response.StatusCode}");
        }

        BankResponse answer = await response.Content.ReadFromJsonAsync<BankResponse>(SnakeCase, cancellationToken)
            ?? throw new BankUnavailableException("bank answered an empty body");
        return answer.Authorized;
    }
}