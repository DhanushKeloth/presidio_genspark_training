using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LogisticShipmentTracking.Models;

[Keyless]
public partial class VwShipmentFull
{
    [Column("id")]
    public int? Id { get; set; }

    [Column("tracking_number")]
    [StringLength(20)]
    public string? TrackingNumber { get; set; }

    [Column("customer_id")]
    public int? CustomerId { get; set; }

    [Column("customer_name")]
    [StringLength(100)]
    public string? CustomerName { get; set; }

    [Column("customer_email", TypeName = "citext")]
    public string? CustomerEmail { get; set; }

    [Column("driver_id")]
    public int? DriverId { get; set; }

    [Column("driver_name")]
    [StringLength(100)]
    public string? DriverName { get; set; }

    [Column("vehicle_type")]
    [StringLength(50)]
    public string? VehicleType { get; set; }

    [Column("license_number")]
    [StringLength(30)]
    public string? LicenseNumber { get; set; }

    [Column("driver_current_lat")]
    public double? DriverCurrentLat { get; set; }

    [Column("driver_current_lng")]
    public double? DriverCurrentLng { get; set; }

    [Column("pickup_address")]
    public string? PickupAddress { get; set; }

    [Column("pickup_lat")]
    public double? PickupLat { get; set; }

    [Column("pickup_lng")]
    public double? PickupLng { get; set; }

    [Column("dropoff_address")]
    public string? DropoffAddress { get; set; }

    [Column("dropoff_lat")]
    public double? DropoffLat { get; set; }

    [Column("dropoff_lng")]
    public double? DropoffLng { get; set; }

    [Column("recipient_name")]
    [StringLength(100)]
    public string? RecipientName { get; set; }

    [Column("recipient_phone")]
    [StringLength(20)]
    public string? RecipientPhone { get; set; }

    [Column("pickup_otp_attempt_count")]
    public short? PickupOtpAttemptCount { get; set; }

    [Column("pickup_otp_expires_at")]
    public DateTime? PickupOtpExpiresAt { get; set; }

    [Column("pickup_otp_generated_at")]
    public DateTime? PickupOtpGeneratedAt { get; set; }

    [Column("pickup_otp_verified_at")]
    public DateTime? PickupOtpVerifiedAt { get; set; }

    [Column("delivery_otp_attempt_count")]
    public short? DeliveryOtpAttemptCount { get; set; }

    [Column("delivery_otp_expires_at")]
    public DateTime? DeliveryOtpExpiresAt { get; set; }

    [Column("delivery_otp_generated_at")]
    public DateTime? DeliveryOtpGeneratedAt { get; set; }

    [Column("delivery_otp_verified_at")]
    public DateTime? DeliveryOtpVerifiedAt { get; set; }

    [Column("total_weight_kg")]
    public decimal? TotalWeightKg { get; set; }

    [Column("item_count")]
    public long? ItemCount { get; set; }

    [Column("picked_up_at")]
    public DateTime? PickedUpAt { get; set; }

    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
