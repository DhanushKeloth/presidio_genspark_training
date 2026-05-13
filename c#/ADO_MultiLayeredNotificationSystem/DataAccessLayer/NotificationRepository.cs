using System.Linq.Expressions;
using Interfaces;
using Models;
using Models.Exceptions;
using Npgsql;
namespace DataAccessLayer
{

    public class NotificationRepository<K, T> : INotificationRepository<K, T> where T : class
    {
        private static string ConnectionString = "Host=localhost;Username=dhanushkeloth;Password=1234;Database=notification_db";

        private readonly List<T> _notifications = new List<T>();
        public T AddNotification(T item)
        {
            // if (item == null)
            // {
            //     Console.WriteLine("cannot add the notification");
            //     throw new NotificationException("Cannot add the notification");
            // }
            // _notifications.Add(item);   
            // return item;
            var notifi = item as Notification;
            string datestring = notifi.sentDate.ToString("u");
            string insertnotificationquery = $"insert into notifications(user_id,message,sentdate) values({notifi.UserId},'{notifi.message}','{datestring}')";
            NpgsqlConnection connection = new NpgsqlConnection(ConnectionString);
            NpgsqlCommand cmd = new NpgsqlCommand(insertnotificationquery, connection);
            try
            {
                connection.Open();
                int rowsinserted = cmd.ExecuteNonQuery();
                if (rowsinserted > 0) Console.WriteLine($"inserted the notifications successfully");

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

        public List<T>? GetNotifications()
        {
            
            NpgsqlConnection connection = new NpgsqlConnection(ConnectionString);
            string sql = "SELECT notification_id, user_id, message, sentdate FROM notifications";
            NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);
            try
            {
                connection.Open();
                NpgsqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    Notification n = new Notification(

                        reader.GetInt32(1),
                        reader.GetString(2),
                        reader.GetDateTime(3)
                    );
                    _notifications.Add((T)(object)n);
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
            return _notifications;
        }

    }
}