using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusBookingAPI.Models.Entities;

[Table("admins")]
public class Admin
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

    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("role")]
    [MaxLength(30)]
    public string Role { get; set; } = "admin";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
