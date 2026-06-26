using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using ShipmentTrackingAPI.Models;
using ShipmentTrackingAPI.Models.Enums;

namespace ShipmentTrackingAPI.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<CustomerProfile> CustomerProfiles { get; set; }

    public virtual DbSet<DriverProfile> DriverProfiles { get; set; }

    public virtual DbSet<SavedAddress> SavedAddresses { get; set; }

    public virtual DbSet<Shipment> Shipments { get; set; }

    public virtual DbSet<ShipmentAddress> ShipmentAddresses { get; set; }

    public virtual DbSet<ShipmentEvent> ShipmentEvents { get; set; }

    public virtual DbSet<ShipmentItem> ShipmentItems { get; set; }

    public virtual DbSet<ShipmentOtpWindow> ShipmentOtpWindows { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("address_type", new[] { "Pickup", "Dropoff" })
            .HasPostgresEnum("driver_account_status", new[] { "PendingApproval", "Active", "Suspended", "Deleted" })
            .HasPostgresEnum("driver_op_status", new[] { "Available", "InTransit", "Offline" })
            .HasPostgresEnum("otp_type", new[] { "Pickup", "Delivery" })
            .HasPostgresEnum("shipment_status", new[] { "Pending", "Assigned", "PickedUp", "InTransit", "Arrived", "Delivered", "Cancelled", "FailedDelivery" })
            .HasPostgresEnum("user_role", new[] { "Customer", "Driver", "Admin" })
            .HasPostgresExtension("citext")
            .HasPostgresExtension("pgcrypto");

        modelBuilder.Entity<User>()
        .Property(u => u.Role)
        .HasColumnType("user_role");

        modelBuilder.Entity<CustomerProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("customer_profiles_pkey");

            entity.ToTable("customer_profiles", tb => tb.HasComment("1:1 vertical partition of users for Customer-specific data. Avoids NULLs on Driver and Admin rows."));

            entity.Property(e => e.AlternatePhoneNumber).HasComment("Backup contact number. Optional.");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.PhoneNumber).HasComment("Primary customer contact. Visible to assigned driver.");
            entity.Property(e => e.ProfileImageUrl).HasComment("Avatar URL. Store object in cloud storage; only the URL lives here.");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.User).WithOne(p => p.CustomerProfile).HasConstraintName("fk_customer_profiles_user");
        });

        modelBuilder.Entity<DriverProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("driver_profiles_pkey");

            entity.ToTable("driver_profiles", tb => tb.HasComment("1:1 vertical partition of users for Driver-specific data. Avoids NULLs on Customer and Admin rows."));

            entity.Property(e => e.ApprovedBy).HasComment("FK to users: the Admin who set account_status = Active.");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CurrentLat).HasComment("Live GPS latitude. Written by GpsSimulationService every 5s during InTransit.");
            entity.Property(e => e.CurrentLng).HasComment("Live GPS longitude. Written by GpsSimulationService every 5s during InTransit.");
            entity.Property(e => e.PhoneNumber).HasComment("Driver contact number. Shown to customer after assignment.");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.VehicleNumber).HasComment("Registration plate. e.g. TS-09-AB-1234. Customer verifies this at pickup.");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.DriverProfileApprovedByNavigations)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_driver_profiles_approved_by");

            entity.HasOne(d => d.User).WithOne(p => p.DriverProfileUser).HasConstraintName("fk_driver_profiles_user");
        });

        modelBuilder.Entity<SavedAddress>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("saved_addresses_pkey");

            entity.ToTable("saved_addresses", tb => tb.HasComment("Customer address book. Many saved addresses per customer_profile. One default enforced by partial unique index."));

            entity.HasIndex(e => e.CustomerId, "uix_saved_addresses_one_default_per_customer")
                .IsUnique()
                .HasFilter("(is_default = true)");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CustomerId).HasComment("FK to customer_profiles.id — not users.id. Scoped to customer profile, not raw user.");
            entity.Property(e => e.IsDefault).HasComment("At most one TRUE per customer. Enforced by uix_saved_addresses_one_default_per_customer.");
            entity.Property(e => e.Label).HasComment("User-friendly label: Home, Office, Parents, Warehouse, etc.");

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.SavedAddresses)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_saved_addresses_customer");
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipments_pkey");

            entity.ToTable("shipments", tb => tb.HasComment("Delivery contract. Addresses in shipment_addresses; OTP state in shipment_otp_windows."));

            entity.HasIndex(e => e.DriverId, "idx_shipments_driver_id").HasFilter("(driver_id IS NOT NULL)");

            entity.Property(e => e.CancelledAt).HasComment("Set when status transitions to Cancelled. NULL for all other statuses.");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.CustomerId).HasComment("The Sender. NOT NULL — a shipment always has an owner.");
            entity.Property(e => e.DeliveredAt).HasComment("Set when Delivery OTP is verified. Legal proof-of-delivery timestamp (POD).");
            entity.Property(e => e.DriverId).HasComment("NULL until a driver self-assigns. ON DELETE SET NULL preserves history.");
            entity.Property(e => e.FailedAt).HasComment("Set when status transitions to FailedDelivery. NULL for all other statuses.");
            entity.Property(e => e.PickedUpAt).HasComment("Set when Pickup OTP is verified. Legal proof-of-pickup timestamp (POP).");
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

            entity.ToTable("shipment_addresses", tb => tb.HasComment("Address rows per shipment. Replaces 8 pickup/dropoff/recipient columns on shipments."));

            entity.Property(e => e.ContactName).HasComment("NULL on Pickup (sender identified via customer_id). Required on Dropoff.");
            entity.Property(e => e.ContactPhone).HasComment("NULL on Pickup. Required on Dropoff — driver contacts recipient on arrival.");

            entity.HasOne(d => d.Shipment).WithMany(p => p.ShipmentAddresses).HasConstraintName("fk_shipment_addresses_shipment");
        });

        modelBuilder.Entity<ShipmentEvent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipment_events_pkey");

            entity.ToTable("shipment_events", tb => tb.HasComment("Append-only audit log. Insert once per transition. Never update or delete rows."));

            entity.Property(e => e.ActorId).HasComment("NULL = system/BackgroundService. Set to user ID for Driver, Customer, and Admin actions.");
            entity.Property(e => e.Latitude).HasComment("GPS snapshot at event time. Accumulates the breadcrumb trail.");
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
            entity.Property(e => e.WeightKg).HasComment("Weight per unit. Total shipment weight = SUM(weight_kg * quantity).");

            entity.HasOne(d => d.Shipment).WithMany(p => p.ShipmentItems).HasConstraintName("fk_shipment_items_shipment");
        });

        modelBuilder.Entity<ShipmentOtpWindow>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shipment_otp_windows_pkey");

            entity.ToTable("shipment_otp_windows", tb => tb.HasComment("OTP state rows. Replaces 6 OTP columns on shipments. One row per type per shipment."));

            entity.Property(e => e.AttemptCount).HasComment("Increments on wrong code. Reset to 0 on regeneration. Hard cap: 3.");
            entity.Property(e => e.GeneratedAt).HasComment("Audit: when the current code was issued or last regenerated.");
            entity.Property(e => e.OtpCode)
                .IsFixedLength()
                .HasComment("NULL when no active window or after successful verification. Never exposed in views.");
            entity.Property(e => e.VerifiedAt).HasComment("Set on success. Never updated after. Permanent proof-of-verification record.");

            entity.HasOne(d => d.Shipment).WithMany(p => p.ShipmentOtpWindows).HasConstraintName("fk_shipment_otp_shipment");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users", tb => tb.HasComment("Central identity for all roles. Single login endpoint, single FK target."));

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Email).HasComment("citext: case-insensitive uniqueness. user@mail.com = USER@MAIL.COM.");
            entity.Property(e => e.FullName).HasComment("Display name for all roles. Authoritative source — not duplicated in profile tables.");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasComment("Soft-delete flag. FALSE = account deactivated; all FK references preserved.");
            entity.Property(e => e.PasswordHash).HasComment("ASP.NET Core Identity PasswordHasher output. Never plain text.");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });
        // modelBuilder.HasPostgresEnum<UserRole>("user_role");
        // modelBuilder.HasPostgresEnum<DriverAccountStatus>("driver_account_status");
        // modelBuilder.HasPostgresEnum<DriverOpStatus>("driver_op_status");
        // modelBuilder.HasPostgresEnum<ShipmentStatus>("shipment_status");
        // modelBuilder.HasPostgresEnum<AddressType>();
        // modelBuilder.HasPostgresEnum<OtpType>("otp_type");
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
