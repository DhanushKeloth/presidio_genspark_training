using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.Models;

/// <summary>
/// OTP state rows. Replaces 6 OTP columns on shipments. One row per type per shipment.
/// </summary>
[Table("shipment_otp_windows")]
[Index("ShipmentId", Name = "idx_shipment_otp_shipment_id")]
public partial class ShipmentOtpWindow
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("shipment_id")]
    public int ShipmentId { get; set; }

    [Column("otp_type")]
    public OtpType OtpType { get; set; }

    /// <summary>
    /// NULL when no active window or after successful verification. Never exposed in views.
    /// </summary>
    [Column("otp_code")]
    [StringLength(4)]
    public string? OtpCode { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [NotMapped]
    public bool IsLocked => AttemptCount >= 3;

    [NotMapped]
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    

    /// <summary>
    /// Increments on wrong code. Reset to 0 on regeneration. Hard cap: 3.
    /// </summary>
    [Column("attempt_count")]
    public short AttemptCount { get; set; }

    /// <summary>
    /// Audit: when the current code was issued or last regenerated.
    /// </summary>
    [Column("generated_at")]
    public DateTime? GeneratedAt { get; set; }

    /// <summary>
    /// Set on success. Never updated after. Permanent proof-of-verification record.
    /// </summary>
    [Column("verified_at")]
    public DateTime? VerifiedAt { get; set; }

    [ForeignKey("ShipmentId")]
    [InverseProperty("ShipmentOtpWindows")]
    [JsonIgnore]
    public virtual Shipment Shipment { get; set; } = null!;
}
