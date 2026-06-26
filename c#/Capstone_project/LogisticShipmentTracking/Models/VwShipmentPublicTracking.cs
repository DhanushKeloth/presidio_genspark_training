using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LogisticShipmentTracking.Models;

[Keyless]
public partial class VwShipmentPublicTracking
{
    [Column("id")]
    public int? Id { get; set; }

    [Column("tracking_number")]
    [StringLength(20)]
    public string? TrackingNumber { get; set; }

    [Column("dropoff_address")]
    public string? DropoffAddress { get; set; }

    [Column("pickup_address")]
    public string? PickupAddress { get; set; }

    [Column("driver_current_lat")]
    public double? DriverCurrentLat { get; set; }

    [Column("driver_current_lng")]
    public double? DriverCurrentLng { get; set; }

    [Column("picked_up_at")]
    public DateTime? PickedUpAt { get; set; }

    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
