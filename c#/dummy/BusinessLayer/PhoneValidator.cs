using System.Text.RegularExpressions;
using Models.Exceptions;
namespace BusinessLayer;
public class PhoneValidator
{
    private static readonly Regex PhoneRegex = new Regex(
        @"^\+?[1-9]\d{1,14}$",
        RegexOptions.Compiled);

    public static void Validate(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ValidException("Phone number cannot be empty for SMS notifications.");

        string cleanedNumber = phoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

        if (!PhoneRegex.IsMatch(cleanedNumber))
            throw new ValidException($"'{phoneNumber}' is not a valid phone number format.");
    }
}