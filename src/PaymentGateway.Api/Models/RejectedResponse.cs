using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PaymentGateway.Api.Models;

public record FieldError(string Field, string Message);

/// <summary>
/// The body of a 400: invalid input never reaches the bank, that is the
/// "Rejected" outcome. Only the field and the reason are listed, never what
/// was submitted (the card number and the CVV would be in it).
/// </summary>
public record RejectedResponse(PaymentStatus Status, IReadOnlyList<FieldError> Errors)
{
    public static RejectedResponse From(ModelStateDictionary modelState)
    {
        List<FieldError> errors = modelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error => new FieldError(FieldName(entry.Key), Message(error.ErrorMessage))))
            .ToList();
        return new RejectedResponse(PaymentStatus.Rejected, errors);
    }

    // A JSON type error ("1050" for an int) carries the CLR type name; the merchant needs neither.
    private static string Message(string message)
    {
        return message.StartsWith("The JSON value could not be converted") ? "is not of the expected type" : message;
    }

    // "Amount" (an annotation) and "$.amount" (a JSON type error) both become "amount".
    private static string FieldName(string key)
    {
        string name = key.StartsWith("$.") ? key[2..] : key == "$" ? "" : key;
        return name.Length > 0 ? char.ToLowerInvariant(name[0]) + name[1..] : name;
    }
}