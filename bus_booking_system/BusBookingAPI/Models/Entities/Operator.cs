using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBookingAPI.Models.Entities;

[Table("operators")]
public class Operator
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("company_name")]
    [MaxLength(150)]
    public string CompanyName { get; set; } = string.Empty;

    [Column("email")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Column("phone")]
    [MaxLength(15)]
    public string Phone { get; set; } = string.Empty;

    [Column("gst_number")]
    [MaxLength(20)]
    public string? GstNumber { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("approved_by")]
    public Guid? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("ApprovedBy")]
    public Admin? ApprovedByAdmin { get; set; }

    public ICollection<Bus> Buses { get; set; } = [];
}
