using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBookingAPI.Models.Entities;

[Table("routes")]
public class Route
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("source_city")]
    [MaxLength(100)]
    public string SourceCity { get; set; } = string.Empty;

    [Column("destination_city")]
    [MaxLength(100)]
    public string DestinationCity { get; set; } = string.Empty;

    [Column("distance_km")]
    public decimal? DistanceKm { get; set; }

    [Column("estimated_hours")]
    public decimal? EstimatedHours { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("CreatedBy")]
    public Admin? CreatedByAdmin { get; set; }

    public ICollection<Bus> Buses { get; set; } = [];
}
