using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Runtime.InteropServices;
using Interfaces;
using Models;
using Models.Exceptions;
using Npgsql;

namespace DataAccessLayer
{
    public class UserRepository<K, T> : IRepository<K, T> where T : class where K : notnull
    {
        private static string ConnectionString = "Host=localhost;Username=dhanushkeloth;Password=1234;Database=notification_db";

        private readonly Dictionary<K, T> _users = new Dictionary<K, T>();
        public T CreateUser(K key, T item)
        {
            NpgsqlConnection connection = new NpgsqlConnection(ConnectionString);
            string createuserquery = "insert into users (name,email,phone_number) values(@name,@email,@phone_number);";
            var user = item as User;
            try
            {
                if (user == null)
                {
                    throw new InvalidOperationException("item is not a valid object");
                }
                connection.Open();
                NpgsqlCommand cmd = new NpgsqlCommand(createuserquery, connection);

                cmd.Parameters.AddWithValue("name", user.name);
                cmd.Parameters.AddWithValue("email", user.email);
                cmd.Parameters.AddWithValue("phone_number", user.phoneNumber);
                int rowsinserted = cmd.ExecuteNonQuery();
                if (rowsinserted > 0)
                {
                    Console.WriteLine("inserted the values successfully");
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
            return item;

            // if (_users.ContainsKey(key))
            // {
            //     Console.WriteLine("user with the same key already exists");
            //     return _users[key];
            // }
            // _users.Add(key,item);
            // return item;
        }
        public T? GetUser(K key)
        {
            NpgsqlConnection connection = new NpgsqlConnection(ConnectionString);
            string getuserquery = $"select * from users where id={key}";
            try
            {
                connection.Open();
                NpgsqlCommand cmd = new NpgsqlCommand(getuserquery, connection);
                NpgsqlDataReader reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var user = new User
                    (

                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3)
                    );
                    return user as T;
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
            throw new UserNotFoundException($"user not found with id{key}");
            // if (_users.ContainsKey(key))
            // {
            //     return _users[key];
            // }
            // return null;
        }
        public List<T>? GetUsers()
        {
            // if(_users.Count==0) return null;
            // var userslist = _users.Values.ToList();
            // return userslist;   
            var usersList = new List<T>();
            DataSet dataSet = new DataSet();

            string fetchusersquery = "select * from users";
            NpgsqlConnection conn = new NpgsqlConnection(ConnectionString);
            NpgsqlDataAdapter adapter = new NpgsqlDataAdapter(fetchusersquery, conn);
            try
            {
                adapter.Fill(dataSet, "UsersTable");
                Console.WriteLine("fetched the data ");
                DataTable table = dataSet.Tables["UsersTable"] ?? throw new Exception("no values found");
                if (table.Rows.Count == 0) return null;
                foreach (DataRow row in table.Rows)
                {
                    var user = new User(
                        row["name"].ToString() ?? "",
                        row["email"].ToString() ?? "",
                        row["phone_number"].ToString() ?? ""
                    );
                    usersList.Add((T)(object)user);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return usersList;
        }
        public T? UpdateUser(K key, T item)
        {
            // if(_users[key]==null)
            //     return null;
            // _users[key]=item;
            // return item;   
            string updateuserquery = "update users set name=@name,email=@email,phone_number=@phone_number where id=@id;";
            var user = item as User;

            NpgsqlConnection connection = new NpgsqlConnection(ConnectionString);
            try
            {
                connection.Open();
                NpgsqlCommand cmd = new NpgsqlCommand(updateuserquery, connection);
                cmd.Parameters.AddWithValue("id", key);
                cmd.Parameters.AddWithValue("name", user.name);
                cmd.Parameters.AddWithValue("email", user.email);
                cmd.Parameters.AddWithValue("phone_number", user.phoneNumber);

                int updatedRows = cmd.ExecuteNonQuery();
                if (updatedRows > 0) Console.WriteLine($"updated the user with id {key}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                connection.Close();
            }
            return item;
        }
        public T? DeleteUser(K key)
        {
            // var user = _users[key];
            // if (user == null)
            // {
            //     return null;
            // }
            // return user;

            T? userToDelete =  GetUser(key);

            if (userToDelete == null)
            {
                throw new UserNotFoundException($"user does not exist with id {key}"); // User doesn't exist, nothing to delete
            }
            string updateuserquery = $"delete from users where id={key}";
            NpgsqlConnection connection = new NpgsqlConnection(ConnectionString);
            NpgsqlCommand cmd = new NpgsqlCommand(updateuserquery, connection);
            try
            {
                connection.Open();
                int deletedrows = cmd.ExecuteNonQuery();
                if (deletedrows > 0) Console.WriteLine($"deleted the user with id {key}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                connection.Close();
            }
            return userToDelete;
        }
    }
}