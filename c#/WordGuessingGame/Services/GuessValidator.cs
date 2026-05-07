using System.Runtime.Serialization;

namespace WordGuessingGame.Services
{

    class GuessValidator
    {
        public void Validate(string inputword)
        {
            if (string.IsNullOrWhiteSpace(inputword))
            {
                throw new InvalidGuessWordException("input should not be empty");
            }
            else if (inputword.Length!=5)
            {
                throw new InvalidGuessWordException("input word should be of length 5");

            }
            else if (!inputword.All(char.IsLetter))
            {
                throw new InvalidGuessWordException("input should be of type string");
            }
        }
    }
}
