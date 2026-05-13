using Interfaces;
using Models;
namespace Notifications
{
    
public class EmailNotification : INotification
{
    public void Send(User user, Notification notification)
    {
        Console.WriteLine("[email notification ]");
        Console.WriteLine($"to : {user.email}");
        Console.WriteLine($"sent at {notification.sentDate}");

        Console.WriteLine($"message:{notification.message}");

    }
}
}