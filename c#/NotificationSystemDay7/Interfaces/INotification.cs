using NotificationSystem.Models;
namespace NotificationSystem.Interfaces
{
    
public interface INotification
{
    public void Send(User user,Notification notification);
}

}