using Npgsql;
using WordGuessingGame.Interfaces;
namespace WordGuessingGame.Repository
{

    public class WordRepository:IWordProvider
    {
        private string ConnectionString = "Host=localhost;Username=dhanushkeloth;Password=12345;Database=word_guess_game";

        public string GetRandomWord()
        {

            NpgsqlConnection connection = new NpgsqlConnection(ConnectionString);
            string getwordquery = "select text from words order by random() limit 1";
            NpgsqlCommand cmd = new NpgsqlCommand(getwordquery, connection);
            string guessword;
            try
            {
                connection.Open();
                guessword = cmd.ExecuteScalar().ToString();
                if (guessword == null)
                {
                    throw new InvalidOperationException("words not found");
                }
                return guessword ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return "GUESS";
            }
            finally
            {
                connection.Close();

            }

        }
    }
}