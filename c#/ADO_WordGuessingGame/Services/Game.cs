using WordGuessingGame.Repository;
using WordGuessingGame.Models;
using WordGuessingGame.Services;
using System.Text.RegularExpressions;
using WordGuessingGame.Interfaces;


class Game
{
    // private WordRepository _wordRepo = new WordRepository();
    private UserRepository _userRepo = new UserRepository();
    private User _currentUser;
    private GuessValidator _validator = new GuessValidator();
    private IWordProvider _wordprovider = new WordRepository();
    private GameSession _session;
    private FeedbackGenerator _feedback = new FeedbackGenerator();
    private string[] _comments =
    {
        "Genius",
        "Excellent",
        "Great work",  
        "Nice Try",
        "That was close!"
    };
    public void start(User user)
    {
        _currentUser = user;
        string secretWord = _wordprovider.GetRandomWord();
        _session = new GameSession(secretWord);
        while (_session.RemainingAttempts > 0 && !_session.IsWon)
        {

            Console.WriteLine($"\nAttempt {6 - _session.RemainingAttempts + 1} of 6");
            string guessword = getValidGuess();
            //feedback generator
            string feedback = _feedback.GenerateFeedback(secretWord, guessword);

            // Console.WriteLine(feedback);
            _session.History.Add(new GuessAttempt(guessword, feedback));
            DisplayBoard();
            if (guessword == _session.SecretWord)
            {
                _session.IsWon = true;
                int index = _session.History.Count - 1;
                int finalscore= _session.RemainingAttempts*10;
                _userRepo.SaveScore(_currentUser.Id,finalscore);
                string winmessage = _comments[index];

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"{winmessage}, You won the game with a score {finalscore}");
                Console.ResetColor();
            }

        }
        if (!_session.IsWon)
        {
            Console.WriteLine($"you lost the game, the word is {_session.SecretWord}");
        }
    }
    private void DisplayBoard()
    {
        Console.WriteLine("\n--- Progress ---");
        foreach (var attempt in _session.History)
        {

            foreach (char c in attempt.Word)
            {
                Console.Write(c + " ");

            }
            Console.WriteLine();

            // Print colored feedback
            foreach (char f in attempt.Feedback)
            {
                if (f == 'G') Console.ForegroundColor = ConsoleColor.Green;
                else if (f == 'Y') Console.ForegroundColor = ConsoleColor.Yellow;
                else Console.ForegroundColor = ConsoleColor.Gray;

                Console.Write(f + " ");
                Console.ResetColor();
            }
            Console.WriteLine();
        }
    }
    public string getValidGuess()
    {
        while (true)
        {

            Console.WriteLine("enter the guess word:");
            string inputword = Console.ReadLine()?.ToUpper()?.Trim() ?? "";
            try
            {
                _validator.Validate(inputword);
                return inputword;
            }
            catch (InvalidGuessWordException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
            }
        }
    }
}