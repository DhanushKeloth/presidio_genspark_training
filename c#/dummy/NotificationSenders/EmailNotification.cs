using Interfaces;
using Models;
namespace Notifications
{
    
public class EmailNotification : INotification
{
    public void Send(User user, Notification notification)
    {
        Console.WriteLine("[email notification ]");
        Console.WriteLine($"to : {user.Email}");
        Console.WriteLine($"sent at {notification.Sentdate}");

        Console.WriteLine($"message:{notification.Message}");

    }
}
}