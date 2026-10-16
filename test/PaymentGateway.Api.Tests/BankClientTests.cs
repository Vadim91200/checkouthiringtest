using System.Net;
using System.Text;
using System.Text.Json.Nodes;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

/// <summary>The bank client against a stubbed handler: request shape and every answer the simulator can give.</summary>
public class BankClientTests
{
    private static PostPaymentRequest Request()
    {
        return new PostPaymentRequest
        {
            CardNumber = "2222405343248877",
            ExpiryMonth = 4,
            ExpiryYear = DateTime.UtcNow.Year + 1,
            Currency = "GBP",
            Amount = 100,
            Cvv = "123"
        };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _answer;

        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer)
        {
            _answer = answer;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _answer(request);
        }
    }

    private static BankClient ClientAnswering(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer)
    {
        return new BankClient(new HttpClient(new StubHandler(answer)) { BaseAddress = new Uri("http://bank/") });
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    [Fact]
    public async Task SendsTheSimulatorContract()
    {
        string? url = null;
        JsonNode? body = null;
        BankClient client = ClientAnswering(async request =>
        {
            url = request.RequestUri!.ToString();
            body = JsonNode.Parse(await request.Content!.ReadAsStringAsync());
            return Json(HttpStatusCode.OK, """{"authorized": true, "authorization_code": "abc"}""");
        });

        Assert.True(await client.AuthorizeAsync(Request(), CancellationToken.None));

        Assert.Equal("http://bank/payments", url);
        JsonNode expected = JsonNode.Parse($$"""
            {
              "card_number": "2222405343248877",
              "expiry_date": "04/{{DateTime.UtcNow.Year + 1}}",
              "currency": "GBP",
              "amount": 100,
              "cvv": "123"
            }
            """)!;
        Assert.True(JsonNode.DeepEquals(expected, body), body!.ToJsonString()); // zero-padded MM/YYYY
    }

    [Fact]
    public async Task DeclinedAnswer()
    {
        BankClient client = ClientAnswering(_ => Task.FromResult(Json(HttpStatusCode.OK, """{"authorized": false, "authorization_code": ""}""")));

        Assert.False(await client.AuthorizeAsync(Request(), CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Non200IsUnavailable(HttpStatusCode status)
    {
        BankClient client = ClientAnswering(_ => Task.FromResult(Json(status, "{}")));

        await Assert.ThrowsAsync<BankUnavailableException>(() => client.AuthorizeAsync(Request(), CancellationToken.None));
    }

    [Fact]
    public async Task NetworkErrorIsUnavailable()
    {
        BankClient client = ClientAnswering(_ => throw new HttpRequestException("refused"));

        await Assert.ThrowsAsync<BankUnavailableException>(() => client.AuthorizeAsync(Request(), CancellationToken.None));
    }

    [Fact]
    public async Task TimeoutIsUnavailable()
    {
        BankClient client = ClientAnswering(_ => throw new TaskCanceledException("timed out"));

        await Assert.ThrowsAsync<BankUnavailableException>(() => client.AuthorizeAsync(Request(), CancellationToken.None));
    }
}