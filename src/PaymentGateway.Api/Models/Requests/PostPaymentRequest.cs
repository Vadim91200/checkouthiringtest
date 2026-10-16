using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Models.Requests;

/// <summary>
/// The merchant's request, with every validation rule of the brief on it. A
/// request that fails them is Rejected before the bank is called.
/// Properties are nullable so that "absent" is reported as "required" rather
/// than as an out-of-range default; they are non-null once validation passed.
/// </summary>
public class PostPaymentRequest : IValidatableObject
{
    // A string, not a number: a PAN keeps its leading zeros and is never arithmetic.
    [Required]
    [RegularExpression(@"^\d{14,19}$", ErrorMessage = "must be 14 to 19 digits")]
    public string? CardNumber { get; set; }

    [Required]
    [Range(1, 12, ErrorMessage = "must be between 1 and 12")]
    public int? ExpiryMonth { get; set; }

    [Required]
    public int? ExpiryYear { get; set; }

    // Three currencies only, as the brief asks. Add a code here to accept it.
    [Required]
    [AllowedValues("GBP", "USD", "EUR", ErrorMessage = "must be one of GBP, USD, EUR")]
    public string? Currency { get; set; }

    // Minor units, so an integer. System.Text.Json already refuses "1050" and 10.5.
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "must be a positive integer, in minor units")]
    public int? Amount { get; set; }

    [Required]
    [RegularExpression(@"^\d{3,4}$", ErrorMessage = "must be 3 or 4 digits")]
    public string? Cvv { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // A card is valid through the last day of its expiry month, so the
        // current month is still accepted (that is what the issuer does too).
        DateTime today = DateTime.UtcNow;
        if (ExpiryYear is int year && ExpiryMonth is int month
            && (year < today.Year || (year == today.Year && month < today.Month)))
        {
            yield return new ValidationResult("card has expired", new[] { nameof(ExpiryYear) });
        }
    }
}