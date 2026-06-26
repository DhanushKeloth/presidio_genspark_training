using NotificationSystem.Interfaces;
namespace NotificationSystem.Repositories
{

   public class NotificationRepository<K,T>: INotificationRepository<K,T> where T:class
    {
        private readonly List<T> _notifications = new List<T>();        
        public T AddNotification(T item)
        {
            if (item == null)
            {
                Console.WriteLine("cannot add the notification");
                return null;
            }
            _notifications.Add(item);   
            return item;
        }
    
        public List<T>? GetNotifications()
        {
            return _notifications;
        }
    }
}