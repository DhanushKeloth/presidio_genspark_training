using Interfaces;
using Models;
namespace Notifications
{
    
public class SMSNotification : INotification
{
    public void Send(User user, Notification notification)
    {
        Console.WriteLine("SMS notification");

        Console.WriteLine($"to: {user.PhoneNumber}");
        Console.WriteLine($"message:{notification.Message}");
        Console.WriteLine($"sent at:{notification.Sentdate}");

    }
}
}