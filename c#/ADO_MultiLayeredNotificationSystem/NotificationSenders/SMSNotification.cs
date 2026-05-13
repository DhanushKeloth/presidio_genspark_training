using Interfaces;
using Models;
namespace Notifications
{
    
public class SMSNotification : INotification
{
    public void Send(User user, Notification notification)
    {
        Console.WriteLine("SMS notification");

        Console.WriteLine($"to: {user.phoneNumber}");
        Console.WriteLine($"message:{notification.message}");
        Console.WriteLine($"sent at:{notification.sentDate}");

    }
}
}