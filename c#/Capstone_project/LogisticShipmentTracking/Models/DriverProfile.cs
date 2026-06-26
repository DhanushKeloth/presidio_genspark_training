using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LogisticShipmentTracking.Models;

/// <summary>
/// 1:1 vertical partition of users for Driver-specific data. Avoids NULLs on non-driver rows.
/// </summary>
[Table("driver_profiles")]
[Index("LicenseNumber", Name = "uq_driver_profiles_license", IsUnique = true)]
[Index("UserId", Name = "uq_driver_profiles_user_id", IsUnique = true)]
public partial class DriverProfile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("vehicle_type")]
    [StringLength(50)]
    public string VehicleType { get; set; } = null!;

    [Column("license_number")]
    [StringLength(30)]
    public string LicenseNumber { get; set; } = null!;

    /// <summary>
    /// Live GPS latitude. Written by GpsSimulationService every 5s during InTransit.
    /// </summary>
    [Column("current_lat")]
    public double? CurrentLat { get; set; }

    /// <summary>
    /// Live GPS longitude. Written by GpsSimulationService every 5s during InTransit.
    /// </summary>
    [Column("current_lng")]
    public double? CurrentLng { get; set; }

    /// <summary>
    /// FK to users: the Admin who set account_status = Active.
    /// </summary>
    [Column("approved_by")]
    public int? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("ApprovedBy")]
    [InverseProperty("DriverProfileApprovedByNavigations")]
    public virtual User? ApprovedByNavigation { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("DriverProfileUser")]
    public virtual User User { get; set; } = null!;
}
