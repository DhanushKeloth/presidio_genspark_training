using System;

namespace LibraryManagementSystem.Exceptions
{
    public class ValidException : Exception
    {
        public ValidException(string message) : base(message) { }
    }
}