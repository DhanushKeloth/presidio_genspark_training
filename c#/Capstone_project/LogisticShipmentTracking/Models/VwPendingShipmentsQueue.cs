using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LogisticShipmentTracking.Models;

[Keyless]
public partial class VwPendingShipmentsQueue
{
    [Column("id")]
    public int? Id { get; set; }

    [Column("tracking_number")]
    [StringLength(20)]
    public string? TrackingNumber { get; set; }

    [Column("pickup_area")]
    public string? PickupArea { get; set; }

    [Column("dropoff_area")]
    public string? DropoffArea { get; set; }

    [Column("pickup_lat")]
    public double? PickupLat { get; set; }

    [Column("pickup_lng")]
    public double? PickupLng { get; set; }

    [Column("dropoff_lat")]
    public double? DropoffLat { get; set; }

    [Column("dropoff_lng")]
    public double? DropoffLng { get; set; }

    [Column("total_weight_kg")]
    public decimal? TotalWeightKg { get; set; }

    [Column("item_count")]
    public long? ItemCount { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
