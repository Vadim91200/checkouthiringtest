using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests.Support;

/// <summary>
/// Stands in for the acquiring bank: answers by the card's last digit like the
/// simulator does, and records every request it was asked to authorize.
/// </summary>
public class FakeBank : IAcquiringBank
{
    public List<PostPaymentRequest> Requests { get; } = new();

    public Task<bool> AuthorizeAsync(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        int last = request.CardNumber![^1] - '0';
        if (last == 0)
        {
            throw new BankUnavailableException("bank answered HTTP 503");
        }

        return Task.FromResult(last % 2 == 1);
    }
}