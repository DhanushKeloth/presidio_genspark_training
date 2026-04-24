using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBookingAPI.Models.Entities;

[Table("payments")]
public class Payment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("booking_id")]
    public Guid BookingId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("payment_method")]
    public string PaymentMethod { get; set; } = "dummy";

    [Column("payment_status")]
    public string PaymentStatus { get; set; } = "pending";

    [Column("stripe_payment_id")]
    [MaxLength(100)]
    public string? StripePaymentId { get; set; }

    [Column("stripe_refund_id")]
    [MaxLength(100)]
    public string? StripeRefundId { get; set; }

    [Column("refund_amount")]
    public decimal? RefundAmount { get; set; }

    [Column("refund_initiated_at")]
    public DateTime? RefundInitiatedAt { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("BookingId")]
    public Booking? Booking { get; set; }
}
