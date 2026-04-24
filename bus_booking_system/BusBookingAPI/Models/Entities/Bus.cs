using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBookingAPI.Models.Entities;

[Table("buses")]
public class Bus
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("operator_id")]
    public Guid OperatorId { get; set; }

    [Column("route_id")]
    public Guid RouteId { get; set; }

    [Column("registration_number")]
    [MaxLength(20)]
    public string RegistrationNumber { get; set; } = string.Empty;

    [Column("bus_type")]
    [MaxLength(20)]
    public string BusType { get; set; } = string.Empty;

    [Column("total_seats")]
    public short TotalSeats { get; set; }

    [Column("departure_time")]
    public TimeOnly DepartureTime { get; set; }

    [Column("arrival_time")]
    public TimeOnly ArrivalTime { get; set; }

    [Column("boarding_location")]
    public string BoardingLocation { get; set; } = string.Empty;

    [Column("dropping_location")]
    public string DroppingLocation { get; set; } = string.Empty;

    [Column("price_per_seat")]
    public decimal PricePerSeat { get; set; }

    [Column("amenities", TypeName = "jsonb")]
    public string Amenities { get; set; } = "[]";

    [Column("seat_layout", TypeName = "jsonb")]
    public string SeatLayout { get; set; } = "{}";

    [Column("status")]
    public string Status { get; set; } = "active";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("OperatorId")]
    public Operator? Operator { get; set; }

    [ForeignKey("RouteId")]
    public Route? Route { get; set; }

    public ICollection<Seat> Seats { get; set; } = [];
    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<SeatLock> SeatLocks { get; set; } = [];
}
