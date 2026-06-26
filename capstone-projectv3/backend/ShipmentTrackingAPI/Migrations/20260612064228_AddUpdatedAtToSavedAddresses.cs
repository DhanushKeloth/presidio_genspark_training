using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using ShipmentTrackingAPI.Models.Enums;

#nullable disable

namespace ShipmentTrackingAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedAtToSavedAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:address_type", "Pickup,Dropoff")
                // .Annotation("Npgsql:Enum:address_type.address_type", "pickup,dropoff")
                .Annotation("Npgsql:Enum:driver_account_status", "PendingApproval,Active,Suspended,Deleted")
                // .Annotation("Npgsql:Enum:driver_account_status.driver_account_status", "pending_approval,active,suspended,deleted")
                .Annotation("Npgsql:Enum:driver_op_status", "Available,InTransit,Offline")
                // .Annotation("Npgsql:Enum:driver_op_status.driver_op_status", "available,in_transit,offline")
                .Annotation("Npgsql:Enum:otp_type", "Pickup,Delivery")
                // .Annotation("Npgsql:Enum:otp_type.otp_type", "pickup,delivery")
                .Annotation("Npgsql:Enum:shipment_status", "Pending,Assigned,PickedUp,InTransit,Arrived,Delivered,Cancelled,FailedDelivery")
                // .Annotation("Npgsql:Enum:shipment_status.shipment_status", "pending,assigned,picked_up,in_transit,arrived,delivered,cancelled,failed_delivery")
                .Annotation("Npgsql:Enum:user_role", "Customer,Driver,Admin")
                // .Annotation("Npgsql:Enum:user_role.user_role", "customer,driver,admin")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    email = table.Column<string>(type: "citext", nullable: false, comment: "citext: case-insensitive uniqueness. user@mail.com = USER@MAIL.COM."),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Display name for all roles. Authoritative source — not duplicated in profile tables."),
                    password_hash = table.Column<string>(type: "text", nullable: false, comment: "ASP.NET Core Identity PasswordHasher output. Never plain text."),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Soft-delete flag. FALSE = account deactivated; all FK references preserved."),
                    role = table.Column<UserRole>(type: "user_role", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.id);
                },
                comment: "Central identity for all roles. Single login endpoint, single FK target.");

            migrationBuilder.CreateTable(
                name: "customer_profiles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Primary customer contact. Visible to assigned driver."),
                    alternate_phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Backup contact number. Optional."),
                    profile_image_url = table.Column<string>(type: "text", nullable: true, comment: "Avatar URL. Store object in cloud storage; only the URL lives here."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("customer_profiles_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_profiles_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "1:1 vertical partition of users for Customer-specific data. Avoids NULLs on Driver and Admin rows.");

            migrationBuilder.CreateTable(
                name: "driver_profiles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Driver contact number. Shown to customer after assignment."),
                    vehicle_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    vehicle_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Registration plate. e.g. TS-09-AB-1234. Customer verifies this at pickup."),
                    license_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    account_status = table.Column<DriverAccountStatus>(type: "driver_account_status", nullable: false),
                    op_status = table.Column<DriverOpStatus>(type: "driver_op_status", nullable: true),
                    current_lat = table.Column<double>(type: "double precision", nullable: true, comment: "Live GPS latitude. Written by GpsSimulationService every 5s during InTransit."),
                    current_lng = table.Column<double>(type: "double precision", nullable: true, comment: "Live GPS longitude. Written by GpsSimulationService every 5s during InTransit."),
                    approved_by = table.Column<int>(type: "integer", nullable: true, comment: "FK to users: the Admin who set account_status = Active."),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("driver_profiles_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_driver_profiles_approved_by",
                        column: x => x.approved_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_driver_profiles_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "1:1 vertical partition of users for Driver-specific data. Avoids NULLs on Customer and Admin rows.");

            migrationBuilder.CreateTable(
                name: "shipments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tracking_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, comment: "Public identifier. Format: TRK-XXXXXX. Generated by TrackingNumberService."),
                    customer_id = table.Column<int>(type: "integer", nullable: false, comment: "The Sender. NOT NULL — a shipment always has an owner."),
                    driver_id = table.Column<int>(type: "integer", nullable: true, comment: "NULL until a driver self-assigns. ON DELETE SET NULL preserves history."),
                    status = table.Column<ShipmentStatus>(type: "shipment_status", nullable: false),
                    picked_up_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Set when Pickup OTP is verified. Legal proof-of-pickup timestamp (POP)."),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Set when Delivery OTP is verified. Legal proof-of-delivery timestamp (POD)."),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Set when status transitions to Cancelled. NULL for all other statuses."),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Set when status transitions to FailedDelivery. NULL for all other statuses."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("shipments_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_shipments_customer",
                        column: x => x.customer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_shipments_driver",
                        column: x => x.driver_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "Delivery contract. Addresses in shipment_addresses; OTP state in shipment_otp_windows.");

            migrationBuilder.CreateTable(
                name: "saved_addresses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_id = table.Column<int>(type: "integer", nullable: false, comment: "FK to customer_profiles.id — not users.id. Scoped to customer profile, not raw user."),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "User-friendly label: Home, Office, Parents, Warehouse, etc."),
                    address_line_1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address_line_2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, comment: "At most one TRUE per customer. Enforced by uix_saved_addresses_one_default_per_customer."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("saved_addresses_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_saved_addresses_customer",
                        column: x => x.customer_id,
                        principalTable: "customer_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Customer address book. Many saved addresses per customer_profile. One default enforced by partial unique index.");

            migrationBuilder.CreateTable(
                name: "shipment_addresses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shipment_id = table.Column<int>(type: "integer", nullable: false),
                    address_type = table.Column<AddressType>(type: "address_type", nullable: false),
                    address_line = table.Column<string>(type: "text", nullable: false),
                    lat = table.Column<double>(type: "double precision", nullable: true),
                    lng = table.Column<double>(type: "double precision", nullable: true),
                    contact_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "NULL on Pickup (sender identified via customer_id). Required on Dropoff."),
                    contact_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "NULL on Pickup. Required on Dropoff — driver contacts recipient on arrival.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("shipment_addresses_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_shipment_addresses_shipment",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Address rows per shipment. Replaces 8 pickup/dropoff/recipient columns on shipments.");

            migrationBuilder.CreateTable(
                name: "shipment_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shipment_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<ShipmentStatus>(type: "shipment_status", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true, comment: "GPS snapshot at event time. Accumulates the breadcrumb trail."),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    actor_id = table.Column<int>(type: "integer", nullable: true, comment: "NULL = system/BackgroundService. Set to user ID for Driver, Customer, and Admin actions."),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()", comment: "Always ORDER BY occurred_at ASC for the tracking timeline.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("shipment_events_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_shipment_events_actor",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_shipment_events_shipment",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Append-only audit log. Insert once per transition. Never update or delete rows.");

            migrationBuilder.CreateTable(
                name: "shipment_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shipment_id = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    weight_kg = table.Column<decimal>(type: "numeric(8,3)", precision: 8, scale: 3, nullable: false, comment: "Weight per unit. Total shipment weight = SUM(weight_kg * quantity)."),
                    length_cm = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    width_cm = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    height_cm = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1, comment: "Count of identical units of this item in the shipment.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("shipment_items_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_shipment_items_shipment",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "1NF child table. One row per distinct item type per booking.");

            migrationBuilder.CreateTable(
                name: "shipment_otp_windows",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shipment_id = table.Column<int>(type: "integer", nullable: false),
                    otp_type = table.Column<OtpType>(type: "otp_type", nullable: false),
                    otp_code = table.Column<string>(type: "character(4)", fixedLength: true, maxLength: 4, nullable: true, comment: "NULL when no active window or after successful verification. Never exposed in views."),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<short>(type: "smallint", nullable: false, comment: "Increments on wrong code. Reset to 0 on regeneration. Hard cap: 3."),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Audit: when the current code was issued or last regenerated."),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Set on success. Never updated after. Permanent proof-of-verification record.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("shipment_otp_windows_pkey", x => x.id);
                    table.ForeignKey(
                        name: "fk_shipment_otp_shipment",
                        column: x => x.shipment_id,
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "OTP state rows. Replaces 6 OTP columns on shipments. One row per type per shipment.");

            migrationBuilder.CreateIndex(
                name: "uq_customer_profiles_user_id",
                table: "customer_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_driver_profiles_approved_by",
                table: "driver_profiles",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "uq_driver_profiles_license",
                table: "driver_profiles",
                column: "license_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_driver_profiles_user_id",
                table: "driver_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_saved_addresses_customer_id",
                table: "saved_addresses",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "uix_saved_addresses_one_default_per_customer",
                table: "saved_addresses",
                column: "customer_id",
                unique: true,
                filter: "(is_default = true)");

            migrationBuilder.CreateIndex(
                name: "idx_shipment_addresses_shipment_id",
                table: "shipment_addresses",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "idx_shipment_events_shipment_id",
                table: "shipment_events",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "IX_shipment_events_actor_id",
                table: "shipment_events",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "idx_shipment_items_shipment_id",
                table: "shipment_items",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "idx_shipment_otp_shipment_id",
                table: "shipment_otp_windows",
                column: "shipment_id");

            migrationBuilder.CreateIndex(
                name: "idx_shipments_customer_id",
                table: "shipments",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "idx_shipments_driver_id",
                table: "shipments",
                column: "driver_id",
                filter: "(driver_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "uq_shipments_tracking_number",
                table: "shipments",
                column: "tracking_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "driver_profiles");

            migrationBuilder.DropTable(
                name: "saved_addresses");

            migrationBuilder.DropTable(
                name: "shipment_addresses");

            migrationBuilder.DropTable(
                name: "shipment_events");

            migrationBuilder.DropTable(
                name: "shipment_items");

            migrationBuilder.DropTable(
                name: "shipment_otp_windows");

            migrationBuilder.DropTable(
                name: "customer_profiles");

            migrationBuilder.DropTable(
                name: "shipments");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
