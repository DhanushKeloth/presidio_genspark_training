using System.Text.RegularExpressions;
using Models.Exceptions;

namespace BusinessLayer;

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