using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ShipmentTrackingAPI.Models;

/// <summary>
/// 1NF child table. One row per distinct item type per booking.
/// </summary>
[Table("shipment_items")]
[Index("ShipmentId", Name = "idx_shipment_items_shipment_id")]
public partial class ShipmentItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("shipment_id")]
    public int ShipmentId { get; set; }

    [Column("description")]
    [StringLength(200)]
    public string Description { get; set; } = null!;

    /// <summary>
    /// Weight per unit. Total shipment weight = SUM(weight_kg * quantity).
    /// </summary>
    [Column("weight_kg")]
    [Precision(8, 3)]
    public decimal WeightKg { get; set; }

    [Column("length_cm")]
    [Precision(6, 1)]
    public decimal? LengthCm { get; set; }

    [Column("width_cm")]
    [Precision(6, 1)]
    public decimal? WidthCm { get; set; }

    [Column("height_cm")]
    [Precision(6, 1)]
    public decimal? HeightCm { get; set; }

    /// <summary>
    /// Count of identical units of this item in the shipment.
    /// </summary>
    [Column("quantity")]
    public int Quantity { get; set; }

    [ForeignKey("ShipmentId")]
    [InverseProperty("ShipmentItems")]
    public virtual Shipment Shipment { get; set; } = null!;
}
