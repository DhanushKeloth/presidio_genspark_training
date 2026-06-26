using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.Models;

/// <summary>
/// 1:1 vertical partition of users for Driver-specific data. Avoids NULLs on Customer and Admin rows.
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

    /// <summary>
    /// Driver contact number. Shown to customer after assignment.
    /// </summary>
    [Column("phone_number")]
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [Column("vehicle_type")]
    [StringLength(50)]
    public string VehicleType { get; set; } = null!;

    /// <summary>
    /// Registration plate. e.g. TS-09-AB-1234. Customer verifies this at pickup.
    /// </summary>
    [Column("vehicle_number")]
    [StringLength(20)]
    public string? VehicleNumber { get; set; }

    [Column("license_number")]
    [StringLength(30)]
    public string LicenseNumber { get; set; } = null!;

    
    [Column("account_status")]
    public DriverAccountStatus AccountStatus { get; set; } = DriverAccountStatus.PendingApproval;

    /// <summary>
    /// NULL until account_status = Active. Toggle: Available / InTransit / Offline.
    /// </summary>
    [Column("op_status")]
    public DriverOpStatus? OpStatus { get; set; }
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
    [JsonIgnore]
    public virtual User? ApprovedByNavigation { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("DriverProfileUser")]
    [JsonIgnore]
    public virtual User User { get; set; } = null!;
}
