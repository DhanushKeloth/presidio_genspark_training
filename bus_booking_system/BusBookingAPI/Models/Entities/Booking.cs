using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBookingAPI.Models.Entities;

[Table("bookings")]
public class Booking
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("bus_id")]
    public Guid BusId { get; set; }

    [Column("journey_date")]
    public DateOnly JourneyDate { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("status")]
    public string Status { get; set; } = "confirmed";

    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("ticket_pdf_path")]
    public string? TicketPdfPath { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [ForeignKey("BusId")]
    public Bus? Bus { get; set; }

    public ICollection<BookingDetail> BookingDetails { get; set; } = [];
    public Payment? Payment { get; set; }
}
