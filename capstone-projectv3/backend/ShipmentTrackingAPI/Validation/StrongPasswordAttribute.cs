using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ShipmentTrackingAPI.Validation
{
    public class StrongPasswordAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var password = value as string;

            // We return Success for null/empty because the [Required] attribute 
            // should be the one responsible for catching missing data.
            if (string.IsNullOrEmpty(password))
            {
                return ValidationResult.Success;
            }

            // The Regex for strong password rules
            var regex = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$");

            if (!regex.IsMatch(password))
            {
                // Uses the custom ErrorMessage if provided in the DTO, otherwise falls back to this default.
                var errorMessage = ErrorMessage ?? "Password must be at least 8 characters long and contain at least one uppercase letter, one lowercase letter, one number, and one special character.";
                return new ValidationResult(errorMessage);
            }

            return ValidationResult.Success;
        }
    }
}