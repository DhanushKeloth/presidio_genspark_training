using NotificationSystem.Interfaces;
using NotificationSystem.Models;
namespace NotificationSystem.Services
{
    
public class NotificationService
    {
        public void SendNotification(INotification inotification, User user, Notification notification)
        {
            inotification.Send(user, notification);
        }
    }
}