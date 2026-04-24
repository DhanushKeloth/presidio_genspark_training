using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBookingAPI.Models.Entities;

[Table("notifications")]
public class Notification
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("recipient_id")]
    public Guid RecipientId { get; set; }

    [Column("recipient_type")]
    [MaxLength(20)]
    public string RecipientType { get; set; } = string.Empty;

    [Column("type")]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    [Column("subject")]
    [MaxLength(255)]
    public string Subject { get; set; } = string.Empty;

    [Column("body")]
    public string Body { get; set; } = string.Empty;

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    [Column("sent_at")]
    public DateTime? SentAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
