using System.ComponentModel.DataAnnotations;
namespace Models
{

    public class Notification
    {
        public string message { get; set; } = string.Empty;
        public DateTime sentDate { get; set; }
        public int UserId{get;set;}
        public Notification(int UserId,string message, DateTime sentDate)
        {
            this.UserId=UserId;
            this.message = message;
            this.sentDate = sentDate;
        }
        public override string ToString()
        {
            return $"{message}, {sentDate}";
        }
    }
}