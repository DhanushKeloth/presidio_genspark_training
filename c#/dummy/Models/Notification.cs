using System.ComponentModel.DataAnnotations;
namespace Models
{

    public class Notification
    {
      public int NotificationId { get; set; }

    public int UserId { get; set; }

    public string Message { get; set; } = null!;

    public DateTime Sentdate { get; set; }

    public virtual User User { get; set; } = null!;
        public Notification(int UserId,string Message, DateTime Sentdate)
        {
            this.UserId=UserId;
            this.Message = Message;
            this.Sentdate = Sentdate;
        }
        public override string ToString()
        {
            return $"{Message}, {Sentdate}";
        }
    }
}