using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public interface IAcquiringBank
{
    /// <summary>True when the bank authorized the payment, false when it declined it.</summary>
    /// <exception cref="BankUnavailableException">The bank gave no decision.</exception>
    Task<bool> AuthorizeAsync(PostPaymentRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// The bank gave no usable answer: timeout, network error, 5xx or an
/// unexpected status. The payment's outcome is unknown and nothing is stored.
/// </summary>
public class BankUnavailableException : Exception
{
    public BankUnavailableException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}