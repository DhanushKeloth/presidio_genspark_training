using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ShipmentTrackingAPI.Models;

/// <summary>
/// Customer address book. Many saved addresses per customer_profile. One default enforced by partial unique index.
/// </summary>
[Table("saved_addresses")]
[Index("CustomerId", Name = "idx_saved_addresses_customer_id")]
public partial class SavedAddress
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// FK to customer_profiles.id — not users.id. Scoped to customer profile, not raw user.
    /// </summary>
    [Column("customer_id")]
    public int CustomerId { get; set; }

    /// <summary>
    /// User-friendly label: Home, Office, Parents, Warehouse, etc.
    /// </summary>
    [Column("label")]
    [StringLength(50)]
    public string Label { get; set; } = null!;

    [Column("address_line_1")]
    [StringLength(200)]
    public string AddressLine1 { get; set; } = null!;

    [Column("address_line_2")]
    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [Column("city")]
    [StringLength(100)]
    public string City { get; set; } = null!;

    [Column("state")]
    [StringLength(100)]
    public string State { get; set; } = null!;

    [Column("postal_code")]
    [StringLength(20)]
    public string PostalCode { get; set; } = null!;

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    /// <summary>
    /// At most one TRUE per customer. Enforced by uix_saved_addresses_one_default_per_customer.
    /// </summary>
    [Column("is_default")]
    public bool IsDefault { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // [Column("updated_at")]
    // public DateTime UpdatedAt { get; set; }

    [ForeignKey("CustomerId")]
    [InverseProperty("SavedAddresses")]
    public virtual CustomerProfile Customer { get; set; } = null!;
}
