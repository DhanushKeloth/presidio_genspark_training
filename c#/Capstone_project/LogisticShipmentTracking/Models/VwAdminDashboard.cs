using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LogisticShipmentTracking.Models;

[Keyless]
public partial class VwAdminDashboard
{
    [Column("shipments_pending")]
    public long? ShipmentsPending { get; set; }

    [Column("shipments_assigned")]
    public long? ShipmentsAssigned { get; set; }

    [Column("shipments_picked_up")]
    public long? ShipmentsPickedUp { get; set; }

    [Column("shipments_in_transit")]
    public long? ShipmentsInTransit { get; set; }

    [Column("shipments_arrived")]
    public long? ShipmentsArrived { get; set; }

    [Column("shipments_delivered")]
    public long? ShipmentsDelivered { get; set; }

    [Column("delivered_today")]
    public long? DeliveredToday { get; set; }

    [Column("drivers_pending_approval")]
    public long? DriversPendingApproval { get; set; }

    [Column("drivers_active")]
    public long? DriversActive { get; set; }

    [Column("drivers_suspended")]
    public long? DriversSuspended { get; set; }

    [Column("total_customers")]
    public long? TotalCustomers { get; set; }
}
