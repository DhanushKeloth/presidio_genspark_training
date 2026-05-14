using Npgsql;
using WordGuessingGame.Models;
using WordGuessingGame.Exceptions;

namespace WordGuessingGame.Repository
{

    public class UserRepository
    {
        private string ConnectionString = "Host=localhost;Username=dhanushkeloth;Password=12345;Database=word_guess_game";

        public User Login(string username, string password)
        {

            NpgsqlConnection connection = new NpgsqlConnection(ConnectionString);
            string loginquery = "SELECT id, username FROM users WHERE username = @username AND password = @password";
            NpgsqlCommand cmd = new NpgsqlCommand(loginquery, connection);
            cmd.Parameters.AddWithValue("username", username);
            cmd.Parameters.AddWithValue("password", password);
            try
            {
                connection.Open();
                NpgsqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new User(Id: reader.GetInt32(0), Username: reader.GetString(1));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                connection.Close();
            }
            throw new UserNotFoundException("user with the details not found");

        }


        public void SaveScore(int userId, int scoreValue)
        {

            NpgsqlConnection connection = new NpgsqlConnection(ConnectionString);
            string insertscorequery = "INSERT INTO scores (user_id, score_value) VALUES (@userid, @scorevalue)";
            NpgsqlCommand cmd = new NpgsqlCommand(insertscorequery, connection);
            cmd.Parameters.AddWithValue("userid", userId);
            cmd.Parameters.AddWithValue("scorevalue", scoreValue);
            try
            {
                connection.Open();
                int insertedrows = cmd.ExecuteNonQuery();
                if (insertedrows > 0)
                {
                    Console.WriteLine("added score successfully");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                connection.Close();
            }
        }
    }
}