using System.Collections.Generic;

namespace WordGuessingGame.Models
{
    public class GameSession
    {
        public string SecretWord { get; set; }
        public List<GuessAttempt> History { get; set; } = new List<GuessAttempt>();
        public bool IsWon { get; set; } = false;
        public int MaxAttempts { get; } = 6;

        public GameSession(string secretWord)
        {
            SecretWord = secretWord;
        }

        public int RemainingAttempts => MaxAttempts - History.Count;
    }
}