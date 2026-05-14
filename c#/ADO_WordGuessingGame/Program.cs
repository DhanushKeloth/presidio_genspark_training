using WordGuessingGame.Exceptions;
using WordGuessingGame.Models;
using WordGuessingGame.Repository;
using WordGuessingGame.Services;
class Program
{
    static void Main(string[] args)
    {
        // WordProvider w = new WordProvider();
        // string hiddenword= w.GetRandomWord();
        // Console.Write(hiddenword);

        // WordRepository wrepo = new WordRepository();
        // string word = wrepo.GetRandomWord();
        // Console.WriteLine(word);

        Console.WriteLine("Word Guessing Game");
        UserRepository userrepo = new UserRepository();
        User? loggedinuser = null;
        while (loggedinuser == null)
        {
            Console.WriteLine("please login to play");
            Console.Write("Username: ");
            string username = Console.ReadLine()?.Trim() ?? "";

            Console.Write("Password: ");
            string password = Console.ReadLine()?.Trim() ?? "";
            try
            {
                loggedinuser = userrepo.Login(username, password);
            }
            catch (UserNotFoundException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        bool running = true;
        while (running)
        {
            Game g = new Game();
            g.start(loggedinuser);
            Console.WriteLine("Do you want to play another game? y/n");
            string choice = Console.ReadLine()?.Trim()?.ToLower() ?? "";
            switch (choice)
            {
                case "y":
                    running = true;
                    break;
                case "n":
                    running = false;
                    break;
                default:
                    Console.WriteLine("invalid input exiting the game");
                    running = false;
                    break;
            }
        }

    }
}
