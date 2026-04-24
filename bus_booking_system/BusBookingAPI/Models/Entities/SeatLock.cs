using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBookingAPI.Models.Entities;

[Table("seat_locks")]
public class SeatLock
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("seat_id")]
    public Guid SeatId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("bus_id")]
    public Guid BusId { get; set; }

    [Column("journey_date")]
    public DateOnly JourneyDate { get; set; }

    [Column("lock_expiry")]
    public DateTime LockExpiry { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("SeatId")]
    public Seat? Seat { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [ForeignKey("BusId")]
    public Bus? Bus { get; set; }
}
