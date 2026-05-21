using System.Text.RegularExpressions;
using LibraryManagementSystem.Exceptions;

namespace LibraryManagementSystem.BusinessLayer
{
    public static class EmailValidator
    {
        private static readonly Regex EmailRegex = new Regex(
            @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static void Validate(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ValidException("Email cannot be empty.");

            if (!EmailRegex.IsMatch(email))
                throw new ValidException($"'{email}' is not a valid email format.");
        }
    }

    public static class PhoneValidator
    {
        private static readonly Regex PhoneRegex = new Regex(
            @"^\+?[1-9]\d{1,14}$",
            RegexOptions.Compiled);

        public static void Validate(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ValidException("Phone number cannot be empty.");

            string cleanedNumber = phoneNumber.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");

            if (!PhoneRegex.IsMatch(cleanedNumber))
                throw new ValidException($"'{phoneNumber}' is not a valid phone number format.");
        }
    }

    public static class UserValidator
    {
        public static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ValidException("Name cannot be empty.");

            if (!Regex.IsMatch(name, @"^[a-zA-Z\s]+$"))
                throw new ValidException("Name should only contain letters.");

            if (name.Length < 2)
                throw new ValidException("Name is too short.");
        }
    }
}