using WordGuessingGame.Interfaces;

namespace WordGuessingGame.Services
{
    public class FeedbackGenerator:IFeedbackGenerator
    {
        public string GenerateFeedback(string secret, string guess)
        {
            char[] feedback = new char[5]; //send the feedback 
            bool[] secretUsed = new bool[5]; //store the bool value for each word after comparision
            bool[] guessUsed = new bool[5]; 

            // Pass 1: Find Greens (Correct Position)
            for (int i = 0; i < 5; i++)
            {
                if (guess[i] == secret[i])
                {
                    feedback[i] = 'G';
                    secretUsed[i] = true;
                    guessUsed[i] = true;
                }
            }

            // Pass 2: Find Yellows (Wrong Position)
            for (int i = 0; i < 5; i++)
            {
                if (guessUsed[i]) continue;

                for (int j = 0; j < 5; j++)
                {
                    if (!secretUsed[j] && guess[i] == secret[j])
                    {
                        feedback[i] = 'Y';
                        secretUsed[j] = true;
                        break;
                    }
                }

                if (feedback[i] == '\0') feedback[i] = 'X';
            }

            return new string(feedback);
        }
    }
}