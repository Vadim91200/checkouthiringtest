using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Models;

/// <summary>What the gateway keeps and returns. Never the full PAN, never the CVV.</summary>
public record Payment(
    Guid Id,
    PaymentStatus Status,
    string CardNumberLastFour,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount)
{
    public static Payment From(PostPaymentRequest request, bool authorized)
    {
        // The request has passed validation, so every property is set.
        return new Payment(
            Guid.NewGuid(),
            authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            request.CardNumber![^4..],
            request.ExpiryMonth!.Value,
            request.ExpiryYear!.Value,
            request.Currency!,
            request.Amount!.Value);
    }
}