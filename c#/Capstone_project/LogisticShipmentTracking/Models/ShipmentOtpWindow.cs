using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LogisticShipmentTracking.Models;

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

    /// <summary>
    /// NULL when no active window. Cleared on successful verification.
    /// </summary>
    [Column("otp_code")]
    [StringLength(4)]
    public string? OtpCode { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Increments on wrong code. Reset to 0 on regeneration. Hard cap: 3.
    /// </summary>
    [Column("attempt_count")]
    public short AttemptCount { get; set; }

    /// <summary>
    /// Audit record of when the current code was issued or last regenerated.
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
    public virtual Shipment Shipment { get; set; } = null!;
}
