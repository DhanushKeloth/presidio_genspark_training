using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ShipmentTrackingAPI.Validation
{
    public class ValidPhoneNumberAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var phoneNumber = value as string;

            if (string.IsNullOrEmpty(phoneNumber))
            {
                return ValidationResult.Success;
            }

            // 1. Sanitize the input (remove spaces, dashes, dots, and parentheses)
            var cleanNumber = Regex.Replace(phoneNumber, @"[\s\-().]", "");

            // 2. Validate exactly 10 digits
            var regex = new Regex(@"^\d{10}$");

            if (!regex.IsMatch(cleanNumber))
            {
                var errorMessage = ErrorMessage ?? "Invalid phone number format. Please enter exactly 10 digits.";
                return new ValidationResult(errorMessage);
            }

            return ValidationResult.Success;
        }
    }
}