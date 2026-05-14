namespace WordGuessingGame.Interfaces
{
    public interface IFeedbackGenerator
    {
        string GenerateFeedback(string secret, string guess);
    }
}