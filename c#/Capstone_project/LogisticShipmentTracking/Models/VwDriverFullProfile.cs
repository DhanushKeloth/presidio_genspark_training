using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LogisticShipmentTracking.Models;

[Keyless]
public partial class VwDriverFullProfile
{
    [Column("driver_profile_id")]
    public int? DriverProfileId { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("email", TypeName = "citext")]
    public string? Email { get; set; }

    [Column("full_name")]
    [StringLength(100)]
    public string? FullName { get; set; }

    [Column("user_is_active")]
    public bool? UserIsActive { get; set; }

    [Column("vehicle_type")]
    [StringLength(50)]
    public string? VehicleType { get; set; }

    [Column("license_number")]
    [StringLength(30)]
    public string? LicenseNumber { get; set; }

    [Column("current_lat")]
    public double? CurrentLat { get; set; }

    [Column("current_lng")]
    public double? CurrentLng { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("approved_by_name")]
    [StringLength(100)]
    public string? ApprovedByName { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
