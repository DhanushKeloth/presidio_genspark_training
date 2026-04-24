using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBookingAPI.Models.Entities;

[Table("booking_details")]
public class BookingDetail
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("booking_id")]
    public Guid BookingId { get; set; }

    [Column("seat_id")]
    public Guid SeatId { get; set; }

    [Column("passenger_name")]
    [MaxLength(100)]
    public string PassengerName { get; set; } = string.Empty;

    [Column("passenger_age")]
    public short PassengerAge { get; set; }

    [Column("passenger_gender")]
    [MaxLength(10)]
    public string PassengerGender { get; set; } = string.Empty;

    [Column("seat_price")]
    public decimal SeatPrice { get; set; }

    [ForeignKey("BookingId")]
    public Booking? Booking { get; set; }

    [ForeignKey("SeatId")]
    public Seat? Seat { get; set; }
}
