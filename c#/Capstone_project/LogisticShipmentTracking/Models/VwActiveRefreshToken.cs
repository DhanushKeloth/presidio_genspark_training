using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace LogisticShipmentTracking.Models;

[Keyless]
public partial class VwActiveRefreshToken
{
    [Column("id")]
    public int? Id { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("token_hash")]
    [StringLength(512)]
    public string? TokenHash { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("device_hint")]
    [StringLength(100)]
    public string? DeviceHint { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
}
