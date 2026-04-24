using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBookingAPI.Models.Entities;

[Table("seats")]
public class Seat
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("bus_id")]
    public Guid BusId { get; set; }

    [Column("seat_number")]
    [MaxLength(10)]
    public string SeatNumber { get; set; } = string.Empty;

    [Column("seat_type")]
    public string SeatType { get; set; } = "seater";

    [Column("row_position")]
    public short RowPosition { get; set; }

    [Column("col_position")]
    public short ColPosition { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [ForeignKey("BusId")]
    public Bus? Bus { get; set; }

    public ICollection<BookingDetail> BookingDetails { get; set; } = [];
    public ICollection<SeatLock> SeatLocks { get; set; } = [];
}
