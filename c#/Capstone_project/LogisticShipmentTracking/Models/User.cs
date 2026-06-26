using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LogisticShipmentTracking.Models;

/// <summary>
/// Central identity for all roles. Single login endpoint, single FK target.
/// </summary>
[Table("users")]
[Index("Email", Name = "uq_users_email", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// citext: case-insensitive uniqueness. user@mail.com = USER@MAIL.COM.
    /// </summary>
    [Column("email", TypeName = "citext")]
    public string Email { get; set; } = null!;

    [Column("full_name")]
    [StringLength(100)]
    public string FullName { get; set; } = null!;

    /// <summary>
    /// ASP.NET Core Identity PasswordHasher output. Never plain text.
    /// </summary>
    [Column("password_hash")]
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Soft-delete flag. FALSE = account deactivated; all FK references preserved.
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [InverseProperty("ApprovedByNavigation")]
    public virtual ICollection<DriverProfile> DriverProfileApprovedByNavigations { get; set; } = new List<DriverProfile>();

    [InverseProperty("User")]
    public virtual DriverProfile? DriverProfileUser { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    [InverseProperty("Customer")]
    public virtual ICollection<Shipment> ShipmentCustomers { get; set; } = new List<Shipment>();

    [InverseProperty("Driver")]
    public virtual ICollection<Shipment> ShipmentDrivers { get; set; } = new List<Shipment>();

    [InverseProperty("Actor")]
    public virtual ICollection<ShipmentEvent> ShipmentEvents { get; set; } = new List<ShipmentEvent>();
}
