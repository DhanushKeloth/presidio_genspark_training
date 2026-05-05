using System.ComponentModel.DataAnnotations;
namespace NotificationSystem.Models
{
    
public class Notification
{
    public string message{get;set;} =string.Empty;
    public DateTime sentDate{get;set;}
    public Notification(string message,DateTime sentDate)
    {
        this.message=message;
        this.sentDate=sentDate;
    }

}
}