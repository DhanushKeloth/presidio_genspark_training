using System.Text.RegularExpressions;
using LibraryManagementSystem.Exceptions;

namespace LibraryManagementSystem.BusinessLayer
{
    public  class BookValidator
    {
        public static void ValidateTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ValidException("Book title cannot be empty.");
            
            if (title.Length < 2)
                throw new ValidException("Book title must be at least 2 characters long.");
        }

        public static void ValidateAuthor(string author)
        {
            if (string.IsNullOrWhiteSpace(author))
                throw new ValidException("Author name cannot be empty.");

            if (!Regex.IsMatch(author, @"^[a-zA-Z\s\.\-]+$"))
                throw new ValidException("Author name contains invalid characters.");
        }

        public static void ValidateIsbn(string isbn)
        {
            if (string.IsNullOrWhiteSpace(isbn))
                throw new ValidException("ISBN cannot be empty.");

            // Cleans dashes or spaces commonly found in ISBN formats
            string cleanIsbn = isbn.Replace("-", "").Replace(" ", "");

            if (!Regex.IsMatch(cleanIsbn, @"^\d{10}(\d{3})?$"))
                throw new ValidException("ISBN must be a valid 10 or 13 digit numeric sequence.");
        }
    }
}