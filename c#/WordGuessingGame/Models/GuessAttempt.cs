namespace WordGuessingGame.Models
{
    public class GuessAttempt
    {
        public string Word { get; set; }
        public string Feedback { get; set; } 

        public GuessAttempt(string word, string feedback)
        {
            Word = word;
            Feedback = feedback;
        }
    }
}