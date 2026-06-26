using System;
using System.Collections.Generic;
using LogisticShipmentTracking.Models;
using Microsoft.EntityFrameworkCore;

namespace LogisticShipmentTracking.Data;

public partial class LogisticsDbContext : DbContext
{
    public LogisticsDbContext(DbContextOptions<LogisticsDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DriverProfile> DriverProfiles { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Shipment> Shipments { get; set; }

    public virtual DbSet<ShipmentAddress> ShipmentAddresses { get; set; }

    public virtual DbSet<ShipmentEvent> ShipmentEvents { get; set; }

    public virtual DbSet<ShipmentItem> ShipmentItems { get; set; }

    public virtual DbSet<ShipmentOtpWindow> ShipmentOtpWindows { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<VwActiveRefreshToken> VwActiveRefreshTokens { get; set; }

    public virtual DbSet<VwAdminDashboard> VwAdminDashboards { get; set; }

    public virtual DbSet<VwDriverFullProfile> VwDriverFullProfiles { get; set; }

    public virtual DbSet<VwPendingShipmentsQueue> VwPendingShipmentsQueues { get; set; }

    public virtual DbSet<VwShipmentFull> VwShipmentFulls { get; set; }

    public virtual DbSet<VwShipmentPublicTracking> VwShipmentPublicTrackings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("address_type", new[] { "Pickup", "Dropoff" })
            .HasPostgresEnum("driver_account_status", new[] { "PendingApproval", "Active", "Suspended", "Deleted" })
            .HasPostgresEnum("driver_op_status", new[] { "Available", "InTransit", "Offline" })
            .HasPostgresEnum("otp_type", new[] { "Pickup", "Delivery" })
            .HasPostgresEnum("shipment_status", new[] { "Pending", "Assigned", "PickedUp", "InTransit", "Arrived", "Delivered" })
            .HasPostgresEnum("user_role", new[] { "Customer", "Driver", "Admin" })
            .HasPostgresExtension("citext")
            .HasPostgresExtension("pgcrypto");

        modelBuilder.Entity<DriverProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("driver_profiles_pkey");

            entity.ToTable("driver_profiles", tb => tb.HasComment("1:1 vertical partition of users for Driver-specific data. Avoids NULLs on non-driver rows."));

            entity.HasIndex(e => e.UserId, "idx_driver_profiles_active_available").HasFilter("((account_status = 'Active'::driver_account_status) AND (op_status = 'Available'::driver_op_status))");

            entity.HasIndex(e => e.CreatedAt, "idx_driver_profiles_pending_approval")
                .IsDescending()
                .HasFilter("(account_status = 'PendingApproval'::driver_account_status)");

            entity.Property(e => e.ApprovedBy).HasComment("FK to users: the Admin who set account_status = Active.");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CurrentLat).HasComment("Live GPS latitude. Written by GpsSimulationService every 5s during InTransit.");
            entity.Property(e => e.CurrentLng).HasComment("Live GPS longitude. Written by GpsSimulationService every 5s during InTransit.");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.DriverProfileApprovedByNavigations)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_driver_profiles_approved_by");

            entity.HasOne(d => d.User).WithOne(p => p.DriverProfileUser).HasConstraintName("fk_driver_profiles_user");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("refresh_tokens_pkey");

            entity.ToTable("refresh_tokens", tb => tb.HasComment("Hashed refresh tokens per user session. Supports multi-device and explicit logout."));

            entity.HasIndex(e => new { e.UserId, e.ExpiresAt }, "idx_refresh_tokens_active")
                .IsDescending(false, true)
                .HasFilter("(is_revoked = false)");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DeviceHint).HasComment("Optional browser/device label for session management UI.");
            entity.Property(e => e.IsRevoked).HasComment("Set TRUE on logout. Checked before issuing a new access token.");
            entity.Property(e => e.TokenHash).HasComment("SHA-256 of raw token. Raw token sent to client; this hash stored in DB.");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens).HasConstraintName("fk_refresh_tokens_user");
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipments_pkey");

            entity.ToTable("shipments", tb => tb.HasComment("Delivery contract. Normalised to 9 columns. Addresses in shipment_addresses; OTP state in shipment_otp_windows."));

            entity.HasIndex(e => e.DriverId, "idx_shipments_driver_id").HasFilter("(driver_id IS NOT NULL)");

            entity.HasIndex(e => e.DriverId, "idx_shipments_in_transit").HasFilter("(status = 'InTransit'::shipment_status)");

            entity.HasIndex(e => e.CreatedAt, "idx_shipments_pending_queue")
                .IsDescending()
                .HasFilter("(status = 'Pending'::shipment_status)");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CustomerId).HasComment("The Sender. NOT NULL — a shipment always has an owner.");
            entity.Property(e => e.DeliveredAt).HasComment("Set when Delivery OTP is verified. Legal proof-of-delivery timestamp.");
            entity.Property(e => e.DriverId).HasComment("NULL until a driver self-assigns. ON DELETE SET NULL preserves history.");
            entity.Property(e => e.PickedUpAt).HasComment("Set when Pickup OTP is verified. Legal proof-of-pickup timestamp.");
            entity.Property(e => e.TrackingNumber).HasComment("Public identifier. Format: TRK-XXXXXX. Generated by TrackingNumberService.");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Customer).WithMany(p => p.ShipmentCustomers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_shipments_customer");

            entity.HasOne(d => d.Driver).WithMany(p => p.ShipmentDrivers)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_shipments_driver");
        });

        modelBuilder.Entity<ShipmentAddress>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipment_addresses_pkey");

            entity.ToTable("shipment_addresses", tb => tb.HasComment("Address rows per shipment. Replaces 8 pickup/dropoff/recipient columns from shipments."));

            entity.HasIndex(e => e.ShipmentId, "idx_shipment_addresses_dropoff").HasFilter("(address_type = 'Dropoff'::address_type)");

            entity.HasIndex(e => e.ShipmentId, "idx_shipment_addresses_pickup").HasFilter("(address_type = 'Pickup'::address_type)");

            entity.Property(e => e.ContactName).HasComment("NULL on Pickup (sender identified via customer_id). Required on Dropoff.");
            entity.Property(e => e.ContactPhone).HasComment("NULL on Pickup. Required on Dropoff — how the driver contacts the recipient.");

            entity.HasOne(d => d.Shipment).WithMany(p => p.ShipmentAddresses).HasConstraintName("fk_shipment_addresses_shipment");
        });

        modelBuilder.Entity<ShipmentEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipment_events_pkey");

            entity.ToTable("shipment_events", tb => tb.HasComment("Append-only audit log. Insert once per transition. Never update or delete rows."));

            entity.HasIndex(e => e.ActorId, "idx_shipment_events_actor_id").HasFilter("(actor_id IS NOT NULL)");

            entity.Property(e => e.ActorId).HasComment("NULL = system/BackgroundService. Set to user ID for Driver and Admin actions.");
            entity.Property(e => e.Latitude).HasComment("GPS snapshot at event time. Accumulates into the breadcrumb trail.");
            entity.Property(e => e.OccurredAt)
                .HasDefaultValueSql("now()")
                .HasComment("Always ORDER BY occurred_at ASC for the tracking timeline.");

            entity.HasOne(d => d.Actor).WithMany(p => p.ShipmentEvents)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_shipment_events_actor");

            entity.HasOne(d => d.Shipment).WithMany(p => p.ShipmentEvents).HasConstraintName("fk_shipment_events_shipment");
        });

        modelBuilder.Entity<ShipmentItem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipment_items_pkey");

            entity.ToTable("shipment_items", tb => tb.HasComment("1NF child table. One row per distinct item type per booking."));

            entity.Property(e => e.Quantity)
                .HasDefaultValue((short)1)
                .HasComment("Count of identical units of this item in the shipment.");
            entity.Property(e => e.WeightKg).HasComment("Weight per unit. Total = SUM(weight_kg * quantity) across all items.");

            entity.HasOne(d => d.Shipment).WithMany(p => p.ShipmentItems).HasConstraintName("fk_shipment_items_shipment");
        });

        modelBuilder.Entity<ShipmentOtpWindow>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipment_otp_windows_pkey");

            entity.ToTable("shipment_otp_windows", tb => tb.HasComment("OTP state rows. Replaces 6 OTP columns on shipments. One row per type per shipment."));

            entity.Property(e => e.AttemptCount).HasComment("Increments on wrong code. Reset to 0 on regeneration. Hard cap: 3.");
            entity.Property(e => e.GeneratedAt).HasComment("Audit record of when the current code was issued or last regenerated.");
            entity.Property(e => e.OtpCode)
                .IsFixedLength()
                .HasComment("NULL when no active window. Cleared on successful verification.");
            entity.Property(e => e.VerifiedAt).HasComment("Set on success. Never updated after. Permanent proof-of-verification record.");

            entity.HasOne(d => d.Shipment).WithMany(p => p.ShipmentOtpWindows).HasConstraintName("fk_shipment_otp_shipment");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users", tb => tb.HasComment("Central identity for all roles. Single login endpoint, single FK target."));

            entity.HasIndex(e => e.Id, "idx_users_inactive").HasFilter("(is_active = false)");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Email).HasComment("citext: case-insensitive uniqueness. user@mail.com = USER@MAIL.COM.");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasComment("Soft-delete flag. FALSE = account deactivated; all FK references preserved.");
            entity.Property(e => e.PasswordHash).HasComment("ASP.NET Core Identity PasswordHasher output. Never plain text.");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<VwActiveRefreshToken>(entity =>
        {
            entity.ToView("vw_active_refresh_tokens");
        });

        modelBuilder.Entity<VwAdminDashboard>(entity =>
        {
            entity.ToView("vw_admin_dashboard");
        });

        modelBuilder.Entity<VwDriverFullProfile>(entity =>
        {
            entity.ToView("vw_driver_full_profile");
        });

        modelBuilder.Entity<VwPendingShipmentsQueue>(entity =>
        {
            entity.ToView("vw_pending_shipments_queue");
        });

        modelBuilder.Entity<VwShipmentFull>(entity =>
        {
            entity.ToView("vw_shipment_full");
        });

        modelBuilder.Entity<VwShipmentPublicTracking>(entity =>
        {
            entity.ToView("vw_shipment_public_tracking");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
