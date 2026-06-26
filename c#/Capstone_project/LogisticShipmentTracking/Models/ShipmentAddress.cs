using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LogisticShipmentTracking.Models;

/// <summary>
/// Address rows per shipment. Replaces 8 pickup/dropoff/recipient columns from shipments.
/// </summary>
[Table("shipment_addresses")]
[Index("ShipmentId", Name = "idx_shipment_addresses_shipment_id")]
public partial class ShipmentAddress
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("shipment_id")]
    public int ShipmentId { get; set; }

    [Column("address_line")]
    public string AddressLine { get; set; } = null!;

    [Column("lat")]
    public double? Lat { get; set; }

    [Column("lng")]
    public double? Lng { get; set; }

    /// <summary>
    /// NULL on Pickup (sender identified via customer_id). Required on Dropoff.
    /// </summary>
    [Column("contact_name")]
    [StringLength(100)]
    public string? ContactName { get; set; }

    /// <summary>
    /// NULL on Pickup. Required on Dropoff — how the driver contacts the recipient.
    /// </summary>
    [Column("contact_phone")]
    [StringLength(20)]
    public string? ContactPhone { get; set; }

    [ForeignKey("ShipmentId")]
    [InverseProperty("ShipmentAddresses")]
    public virtual Shipment Shipment { get; set; } = null!;
}
