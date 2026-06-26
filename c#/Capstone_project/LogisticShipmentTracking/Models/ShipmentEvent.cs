using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LogisticShipmentTracking.Models;

/// <summary>
/// Append-only audit log. Insert once per transition. Never update or delete rows.
/// </summary>
[Table("shipment_events")]
[Index("ShipmentId", Name = "idx_shipment_events_shipment_id")]
[Index("ShipmentId", "OccurredAt", Name = "idx_shipment_events_timeline")]
public partial class ShipmentEvent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("shipment_id")]
    public int ShipmentId { get; set; }

    [Column("description")]
    public string Description { get; set; } = null!;

    /// <summary>
    /// GPS snapshot at event time. Accumulates into the breadcrumb trail.
    /// </summary>
    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    /// <summary>
    /// NULL = system/BackgroundService. Set to user ID for Driver and Admin actions.
    /// </summary>
    [Column("actor_id")]
    public int? ActorId { get; set; }

    /// <summary>
    /// Always ORDER BY occurred_at ASC for the tracking timeline.
    /// </summary>
    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    [ForeignKey("ActorId")]
    [InverseProperty("ShipmentEvents")]
    public virtual User? Actor { get; set; }

    [ForeignKey("ShipmentId")]
    [InverseProperty("ShipmentEvents")]
    public virtual Shipment Shipment { get; set; } = null!;
}
