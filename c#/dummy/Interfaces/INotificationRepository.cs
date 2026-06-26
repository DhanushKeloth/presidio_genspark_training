namespace Interfaces
{
    public interface INotificationRepository<K,T> where T: class
    {
        public T AddNotification(T item);
    
        public List<T>? GetNotifications();
 
        
    }
}