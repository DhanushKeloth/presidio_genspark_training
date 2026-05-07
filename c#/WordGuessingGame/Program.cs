using WordGuessingGame.Services;
class Program
{
    static void Main(string[] args)
    {
        // WordProvider w = new WordProvider();
        // string hiddenword= w.GetRandomWord();
        // Console.Write(hiddenword);
        Console.WriteLine("Word Guessing Game");

        // g.start();
        // Console.WriteLine(choice);
        bool running = true;
        while (running)
        {
            Game g = new Game();
            g.start();
            Console.WriteLine("Do you want to play another game? y/n" );
            string choice = Console.ReadLine()?.Trim()?.ToLower()??"";
            switch (choice)
            {
                case "y":
                    running=true;
                    break;
                case "n":
                    running=false;
                    break;
                default:
                    Console.WriteLine("invalid input exiting the game");
                    running=false;
                    break;
            }
        }

    }   
}
