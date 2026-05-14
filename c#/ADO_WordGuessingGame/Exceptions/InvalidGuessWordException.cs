using System;
class InvalidGuessWordException: Exception
{
    public InvalidGuessWordException() : base()
    {
        
    }
    public InvalidGuessWordException(string message) : base(message)
    {
        
    }
}