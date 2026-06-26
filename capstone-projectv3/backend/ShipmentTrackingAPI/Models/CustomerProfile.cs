using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ShipmentTrackingAPI.Models;

/// <summary>
/// 1:1 vertical partition of users for Customer-specific data. Avoids NULLs on Driver and Admin rows.
/// </summary>
[Table("customer_profiles")]
[Index("UserId", Name = "uq_customer_profiles_user_id", IsUnique = true)]
public partial class CustomerProfile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    /// <summary>
    /// Primary customer contact. Visible to assigned driver.
    /// </summary>
    [Column("phone_number")]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Backup contact number. Optional.
    /// </summary>
    [Column("alternate_phone_number")]
    [StringLength(20)]
    public string? AlternatePhoneNumber { get; set; }

    /// <summary>
    /// Avatar URL. Store object in cloud storage; only the URL lives here.
    /// </summary>
    [Column("profile_image_url")]
    public string? ProfileImageUrl { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Customer")]
    public virtual ICollection<SavedAddress> SavedAddresses { get; set; } = new List<SavedAddress>();

    [ForeignKey("UserId")]
    [InverseProperty("CustomerProfile")]
    public virtual User User { get; set; } = null!;
}
