using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBookingAPI.Models.Entities;

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("full_name")]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Column("email")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Column("email_verified")]
    public bool EmailVerified { get; set; } = false;

    [Column("phone")]
    [MaxLength(15)]
    public string? Phone { get; set; }

    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("age")]
    public short? Age { get; set; }

    [Column("gender")]
    [MaxLength(10)]
    public string? Gender { get; set; }

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "active";

    [Column("reset_token")]
    public string? ResetToken { get; set; }

    [Column("reset_token_expiry")]
    public DateTime? ResetTokenExpiry { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<SeatLock> SeatLocks { get; set; } = [];
}
