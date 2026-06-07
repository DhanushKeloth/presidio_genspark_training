-- ================================================================
--  SWIFTPARCEL — CAPSTONE DATABASE SCHEMA
--  Version      : 4.1 (JWT-only auth; no refresh tokens)
--  PostgreSQL   : 15+
--  Approach     : DB-First → scaffold into EF Core 8
--  Normalisation: 3NF throughout; BCNF where applicable
--
--  Changes from v4.0:
--    - Dropped refresh_tokens table — JWT-only auth for capstone
--    - Dropped idx_refresh_tokens_user_id (table gone)
--    - Auth is stateless: JWTs verified by signature only, no DB lookup
--
--  Changes from v3.0 (carried forward):
--    - Dropped all 6 views (vw_*) — use EF Core LINQ/.Include() instead
--    - Dropped 16 indexes (partial + composite) — overkill at capstone scale
--    - Dropped trg_customer_profiles_updated_at
--    - Dropped trg_driver_profiles_updated_at
--    - Retained idx_users_role (kept — used by admin user-listing queries)
--    - Retained idx_saved_addresses_customer_id (kept — FK join on address book)
--    - Retained idx_driver_profiles_account_status (kept — approval queue filter)
--    - Retained uix_saved_addresses_one_default_per_customer (kept — data integrity)
--    - Retained idx_shipment_addresses_shipment_id (kept — FK join)
--
--  Execution order (run top to bottom, once):
--    1. Extensions
--    2. Enum types
--    3. Tables (dependency order)
--    4. Indexes
--    5. Triggers
--    6. Seed data
--
--  EF Core scaffold command (run after applying this schema):
--    dotnet ef dbcontext scaffold \
--      "Host=localhost;Database=swiftparcel;Username=postgres;Password=<pwd>" \
--      Npgsql.EntityFrameworkCore.PostgreSQL \
--      --output-dir Models \
--      --context-dir Data \
--      --context AppDbContext \
--      --data-annotations \
--      --no-onconfiguring
-- ================================================================


-- ================================================================
--  SECTION 1 — EXTENSIONS
-- ================================================================

CREATE EXTENSION IF NOT EXISTS "pgcrypto";
-- gen_random_uuid() for future UUID support; crypt() for hashing

CREATE EXTENSION IF NOT EXISTS "citext";
-- Case-insensitive text; used for email to prevent duplicate
-- registrations that differ only in letter case.


-- ================================================================
--  SECTION 2 — ENUM TYPES
--
--  All application-level state enums defined here centrally.
--  EF Core scaffolds these as C# enum properties automatically.
--  Never store status as VARCHAR — the DB must reject invalid values.
-- ================================================================

-- ── 2.1  User roles ─────────────────────────────────────────────
CREATE TYPE user_role AS ENUM (
    'Customer',   -- Books shipments, receives OTPs
    'Driver',     -- Accepts jobs, verifies OTPs, delivers
    'Admin'       -- Manages drivers, monitors platform
);

-- ── 2.2  Driver account lifecycle (Admin-controlled) ────────────
CREATE TYPE driver_account_status AS ENUM (
    'PendingApproval',  -- Default on registration; cannot operate
    'Active',           -- Can toggle availability and accept jobs
    'Suspended',        -- Temporarily blocked; data preserved
    'Deleted'           -- Soft-deleted; data preserved for audit
);

-- ── 2.3  Driver operational availability (Driver-controlled) ────
CREATE TYPE driver_op_status AS ENUM (
    'Available',    -- In the job pool; can accept pending shipments
    'InTransit',    -- Currently fulfilling a delivery
    'Offline'       -- Not accepting jobs
);

-- ── 2.4  Shipment lifecycle status ──────────────────────────────
--
--  Normal path (enforced at service layer):
--    Pending → Assigned → PickedUp → InTransit → Arrived → Delivered
--
--  Exception paths:
--    Pending              → Cancelled     (Customer cancels before assignment,
--                                          or Admin cancels at any stage)
--    Assigned             → Cancelled     (Admin only — driver never cancels)
--    Arrived              → FailedDelivery (Driver cannot complete delivery;
--                                           recipient absent, OTP refused, etc.)
--    Any non-terminal     → Cancelled     (Admin override only)
--
--  Terminal states (no further transitions permitted):
--    Delivered, Cancelled, FailedDelivery
--
CREATE TYPE shipment_status AS ENUM (
    'Pending',          -- Booked; awaiting driver assignment
    'Assigned',         -- Driver claimed; navigating to pickup
    'PickedUp',         -- Pickup OTP verified; parcel collected — POP confirmed
    'InTransit',        -- Driver driving; GPS simulation active
    'Arrived',          -- Driver at destination; awaiting delivery OTP
    'Delivered',        -- Delivery OTP verified; terminal state — POD confirmed
    'Cancelled',        -- Cancelled by Customer (Pending only) or Admin (any stage)
    'FailedDelivery'    -- Driver could not complete delivery; Admin must intervene
);

-- ── 2.5  Address type (used by shipment_addresses) ──────────────
CREATE TYPE address_type AS ENUM (
    'Pickup',   -- Collection point; sender's location
    'Dropoff'   -- Delivery destination; recipient's location
);

-- ── 2.6  OTP type (used by shipment_otp_windows) ────────────────
CREATE TYPE otp_type AS ENUM (
    'Pickup',   -- Verified by Sender before Driver collects parcel
    'Delivery'  -- Verified by Recipient before Driver delivers parcel
);


-- ================================================================
--  SECTION 3 — TABLES
--
--  Creation order respects FK dependencies:
--    users
--    → customer_profiles
--      → saved_addresses
--    → driver_profiles
--    → shipments
--      → shipment_addresses
--      → shipment_items
--      → shipment_otp_windows
--      → shipment_events
-- ================================================================


-- ────────────────────────────────────────────────────────────────
--  3.1  users
--
--  Central identity table. All three roles share one table,
--  differentiated by the role enum. This is intentional — a
--  single login endpoint and a single FK target for all
--  ownership relationships.
--
--  full_name is kept here (not only in profile tables) because:
--  • Admin has no profile table; it needs a name somewhere.
--  • All roles need a display name for event logs and views.
--  • customer_profiles does NOT duplicate full_name — the name
--    set at registration on users is the authoritative value.
--
--  Normalisation:
--  • 1NF: all columns atomic, every row uniquely identified by id.
--  • 2NF: no partial dependencies (single-column PK, trivially met).
--  • 3NF: no transitive dependencies.
--  • Role-specific columns live in their own profile tables.
-- ────────────────────────────────────────────────────────────────
CREATE TABLE users (
    id              SERIAL          PRIMARY KEY,
    email           CITEXT          NOT NULL,
    full_name       VARCHAR(100)    NOT NULL,
    password_hash   TEXT            NOT NULL,
    role            user_role       NOT NULL,
    is_active       BOOLEAN         NOT NULL    DEFAULT TRUE,
    created_at      TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),
    updated_at      TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),

    CONSTRAINT uq_users_email
        UNIQUE (email),

    CONSTRAINT chk_users_full_name_not_blank
        CHECK (LENGTH(TRIM(full_name)) > 0),

    CONSTRAINT chk_users_password_hash_not_blank
        CHECK (LENGTH(password_hash) > 0)
);

COMMENT ON TABLE  users               IS 'Central identity for all roles. Single login endpoint, single FK target.';
COMMENT ON COLUMN users.email         IS 'citext: case-insensitive uniqueness. user@mail.com = USER@MAIL.COM.';
COMMENT ON COLUMN users.full_name     IS 'Display name for all roles. Authoritative source — not duplicated in profile tables.';
COMMENT ON COLUMN users.password_hash IS 'ASP.NET Core Identity PasswordHasher output. Never plain text.';
COMMENT ON COLUMN users.is_active     IS 'Soft-delete flag. FALSE = account deactivated; all FK references preserved.';


-- ────────────────────────────────────────────────────────────────
--  3.2  customer_profiles
--
--  1:1 vertical partition of users for Customer-specific data.
--  Separating these columns from users avoids:
--  • NULL columns on every Driver and Admin row.
--  • Mixing contact details (phone) with authentication data.
--
--  full_name is NOT duplicated here — it lives on users.
--  phone_number and profile_image_url are customer-only concerns.
--
--  UNIQUE (user_id) enforces the 1:1 cardinality at DB level.
--  Created automatically on Customer registration alongside the
--  users row, in the same transaction.
-- ────────────────────────────────────────────────────────────────
CREATE TABLE customer_profiles (
    id                      SERIAL          PRIMARY KEY,
    user_id                 INT             NOT NULL,
    phone_number            VARCHAR(20)     NULL,
    alternate_phone_number  VARCHAR(20)     NULL,
    profile_image_url       TEXT            NULL,
    created_at              TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),
    updated_at              TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),

    -- ── Uniqueness ────────────────────────────────────────────
    CONSTRAINT uq_customer_profiles_user_id
        UNIQUE (user_id),

    -- ── Foreign keys ──────────────────────────────────────────
    CONSTRAINT fk_customer_profiles_user
        FOREIGN KEY (user_id)
        REFERENCES users (id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    -- ── Business rules ────────────────────────────────────────
    CONSTRAINT chk_customer_phone_format
        CHECK (phone_number IS NULL OR LENGTH(TRIM(phone_number)) >= 7),

    CONSTRAINT chk_customer_alt_phone_format
        CHECK (alternate_phone_number IS NULL OR LENGTH(TRIM(alternate_phone_number)) >= 7),

    CONSTRAINT chk_customer_image_url_not_blank
        CHECK (profile_image_url IS NULL OR LENGTH(TRIM(profile_image_url)) > 0)
);

COMMENT ON TABLE  customer_profiles                        IS '1:1 vertical partition of users for Customer-specific data. Avoids NULLs on Driver and Admin rows.';
COMMENT ON COLUMN customer_profiles.phone_number           IS 'Primary customer contact. Visible to assigned driver.';
COMMENT ON COLUMN customer_profiles.alternate_phone_number IS 'Backup contact number. Optional.';
COMMENT ON COLUMN customer_profiles.profile_image_url      IS 'Avatar URL. Store object in cloud storage; only the URL lives here.';


-- ────────────────────────────────────────────────────────────────
--  3.3  saved_addresses
--
--  Customer's personal address book. Many rows per customer.
--  Used to pre-fill the booking form pickup/dropoff fields.
--
--  is_default: at most one TRUE per customer. Enforced via
--  partial unique index (not a table constraint — PostgreSQL
--  cannot express conditional uniqueness as a CHECK; it must
--  be a partial UNIQUE INDEX).
--
--  FK references customer_profiles.id (not users.id) because
--  a saved address belongs to a customer profile, not a raw
--  user account. Cascade delete removes addresses when the
--  customer profile is removed.
-- ────────────────────────────────────────────────────────────────
CREATE TABLE saved_addresses (
    id              SERIAL              PRIMARY KEY,
    customer_id     INT                 NOT NULL,
    label           VARCHAR(50)         NOT NULL,
    address_line_1  VARCHAR(200)        NOT NULL,
    address_line_2  VARCHAR(200)        NULL,
    city            VARCHAR(100)        NOT NULL,
    state           VARCHAR(100)        NOT NULL,
    postal_code     VARCHAR(20)         NOT NULL,
    latitude        DOUBLE PRECISION    NULL,
    longitude       DOUBLE PRECISION    NULL,
    is_default      BOOLEAN             NOT NULL    DEFAULT FALSE,
    created_at      TIMESTAMPTZ         NOT NULL    DEFAULT NOW(),

    -- ── Foreign keys ──────────────────────────────────────────
    CONSTRAINT fk_saved_addresses_customer
        FOREIGN KEY (customer_id)
        REFERENCES customer_profiles (id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    -- ── Business rules ────────────────────────────────────────
    CONSTRAINT chk_saved_address_label_not_blank
        CHECK (LENGTH(TRIM(label)) > 0),

    CONSTRAINT chk_saved_address_line1_not_blank
        CHECK (LENGTH(TRIM(address_line_1)) > 0),

    CONSTRAINT chk_saved_address_city_not_blank
        CHECK (LENGTH(TRIM(city)) > 0),

    CONSTRAINT chk_saved_address_state_not_blank
        CHECK (LENGTH(TRIM(state)) > 0),

    CONSTRAINT chk_saved_address_postal_not_blank
        CHECK (LENGTH(TRIM(postal_code)) > 0),

    -- Coordinates: both set or both null — never one without the other
    CONSTRAINT chk_saved_address_coords_both_or_neither
        CHECK (
            (latitude IS NULL AND longitude IS NULL) OR
            (latitude IS NOT NULL AND longitude IS NOT NULL)
        ),

    CONSTRAINT chk_saved_address_lat_range
        CHECK (latitude  IS NULL OR latitude  BETWEEN -90  AND 90),

    CONSTRAINT chk_saved_address_lng_range
        CHECK (longitude IS NULL OR longitude BETWEEN -180 AND 180)
);

COMMENT ON TABLE  saved_addresses             IS 'Customer address book. Many saved addresses per customer_profile. One default enforced by partial unique index.';
COMMENT ON COLUMN saved_addresses.customer_id IS 'FK to customer_profiles.id — not users.id. Scoped to customer profile, not raw user.';
COMMENT ON COLUMN saved_addresses.label       IS 'User-friendly label: Home, Office, Parents, Warehouse, etc.';
COMMENT ON COLUMN saved_addresses.is_default  IS 'At most one TRUE per customer. Enforced by uix_saved_addresses_one_default_per_customer.';


-- ────────────────────────────────────────────────────────────────
--  3.4  driver_profiles
--
--  1:1 vertical partition of users for Driver-specific data.
--  Separating these columns from users avoids:
--  • 8 permanent NULL columns on every Customer and Admin row.
--  • A partial dependency where columns only apply when role = Driver.
--
--  UNIQUE (user_id) enforces the 1:1 cardinality at DB level.
--  approved_by FK records the Admin who activated this driver.
--
--  phone_number: driver contact visible to customer post-assignment.
--  vehicle_number: registration plate for customer verification.
-- ────────────────────────────────────────────────────────────────
CREATE TABLE driver_profiles (
    id              SERIAL                  PRIMARY KEY,
    user_id         INT                     NOT NULL,
    phone_number    VARCHAR(20)             NULL,
    vehicle_type    VARCHAR(50)             NOT NULL,
    vehicle_number  VARCHAR(20)             NULL,
    license_number  VARCHAR(30)             NOT NULL,
    account_status  driver_account_status   NOT NULL    DEFAULT 'PendingApproval',
    op_status       driver_op_status        NULL,
    current_lat     DOUBLE PRECISION        NULL,
    current_lng     DOUBLE PRECISION        NULL,
    approved_by     INT                     NULL,
    approved_at     TIMESTAMPTZ             NULL,
    created_at      TIMESTAMPTZ             NOT NULL    DEFAULT NOW(),
    updated_at      TIMESTAMPTZ             NOT NULL    DEFAULT NOW(),

    -- ── Uniqueness ────────────────────────────────────────────
    CONSTRAINT uq_driver_profiles_user_id
        UNIQUE (user_id),

    CONSTRAINT uq_driver_profiles_license
        UNIQUE (license_number),

    -- ── Foreign keys ──────────────────────────────────────────
    CONSTRAINT fk_driver_profiles_user
        FOREIGN KEY (user_id)
        REFERENCES users (id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_driver_profiles_approved_by
        FOREIGN KEY (approved_by)
        REFERENCES users (id)
        ON DELETE SET NULL
        ON UPDATE CASCADE,

    -- ── Business rules ────────────────────────────────────────
    CONSTRAINT chk_driver_phone_format
        CHECK (phone_number IS NULL OR LENGTH(TRIM(phone_number)) >= 7),

    CONSTRAINT chk_driver_vehicle_type_not_blank
        CHECK (LENGTH(TRIM(vehicle_type)) > 0),

    CONSTRAINT chk_driver_vehicle_number_not_blank
        CHECK (vehicle_number IS NULL OR LENGTH(TRIM(vehicle_number)) > 0),

    CONSTRAINT chk_driver_license_not_blank
        CHECK (LENGTH(TRIM(license_number)) > 0),

    -- GPS coordinates: valid range if provided
    CONSTRAINT chk_driver_lat_range
        CHECK (current_lat IS NULL OR current_lat BETWEEN -90 AND 90),

    CONSTRAINT chk_driver_lng_range
        CHECK (current_lng IS NULL OR current_lng BETWEEN -180 AND 180),

    -- Approval audit: both timestamp and approver must be set together
    CONSTRAINT chk_driver_approval_consistency
        CHECK (
            (approved_at IS NULL AND approved_by IS NULL) OR
            (approved_at IS NOT NULL AND approved_by IS NOT NULL)
        ),

    -- op_status may only be set when account is Active
    -- (enforced at service layer; CHECK here as DB-level safety net)
    CONSTRAINT chk_driver_op_status_requires_active
        CHECK (
            op_status IS NULL OR account_status = 'Active'
        )
);

COMMENT ON TABLE  driver_profiles                IS '1:1 vertical partition of users for Driver-specific data. Avoids NULLs on Customer and Admin rows.';
COMMENT ON COLUMN driver_profiles.phone_number   IS 'Driver contact number. Shown to customer after assignment.';
COMMENT ON COLUMN driver_profiles.vehicle_number IS 'Registration plate. e.g. TS-09-AB-1234. Customer verifies this at pickup.';
COMMENT ON COLUMN driver_profiles.op_status      IS 'NULL until account_status = Active. Toggle: Available / InTransit / Offline.';
COMMENT ON COLUMN driver_profiles.current_lat    IS 'Live GPS latitude. Written by GpsSimulationService every 5s during InTransit.';
COMMENT ON COLUMN driver_profiles.current_lng    IS 'Live GPS longitude. Written by GpsSimulationService every 5s during InTransit.';
COMMENT ON COLUMN driver_profiles.approved_by    IS 'FK to users: the Admin who set account_status = Active.';


-- ────────────────────────────────────────────────────────────────
--  3.5  shipments  (was 3.6)
--
--  Core delivery contract. Normalised to the minimum required
--  columns. All address data → shipment_addresses. All OTP
--  state → shipment_otp_windows.
--
--  What remains on this table:
--  • Identity        : tracking_number
--  • Ownership       : customer_id, driver_id
--  • Contract state  : status
--  • Legal timestamps: picked_up_at, delivered_at,
--                      cancelled_at, failed_at
--  • Audit           : created_at, updated_at
--
--  Terminal states: Delivered, Cancelled, FailedDelivery.
--  The service layer state machine guard rejects any further
--  transition out of these states (except Admin override).
-- ────────────────────────────────────────────────────────────────
CREATE TABLE shipments (
    id                  SERIAL              PRIMARY KEY,
    tracking_number     VARCHAR(20)         NOT NULL,
    customer_id         INT                 NOT NULL,
    driver_id           INT                 NULL,
    status              shipment_status     NOT NULL    DEFAULT 'Pending',
    picked_up_at        TIMESTAMPTZ         NULL,
    delivered_at        TIMESTAMPTZ         NULL,
    cancelled_at        TIMESTAMPTZ         NULL,
    failed_at           TIMESTAMPTZ         NULL,
    created_at          TIMESTAMPTZ         NOT NULL    DEFAULT NOW(),
    updated_at          TIMESTAMPTZ         NOT NULL    DEFAULT NOW(),

    -- ── Uniqueness ────────────────────────────────────────────
    CONSTRAINT uq_shipments_tracking_number
        UNIQUE (tracking_number),

    -- ── Foreign keys ──────────────────────────────────────────
    CONSTRAINT fk_shipments_customer
        FOREIGN KEY (customer_id)
        REFERENCES users (id)
        ON DELETE RESTRICT          -- Never silently delete a user with shipments
        ON UPDATE CASCADE,

    CONSTRAINT fk_shipments_driver
        FOREIGN KEY (driver_id)
        REFERENCES users (id)
        ON DELETE SET NULL          -- Deleting driver preserves shipment history
        ON UPDATE CASCADE,

    -- ── Business rules ────────────────────────────────────────
    CONSTRAINT chk_tracking_number_format
        CHECK (tracking_number ~ '^TRK-[A-Z0-9]{6}$'),

    CONSTRAINT chk_pickedup_after_created
        CHECK (picked_up_at IS NULL OR picked_up_at >= created_at),

    CONSTRAINT chk_delivered_after_pickedup
        CHECK (
            delivered_at IS NULL OR
            picked_up_at IS NULL OR
            delivered_at >= picked_up_at
        ),

    CONSTRAINT chk_cancelled_after_created
        CHECK (cancelled_at IS NULL OR cancelled_at >= created_at),

    CONSTRAINT chk_failed_after_pickedup
        CHECK (
            failed_at IS NULL OR
            picked_up_at IS NULL OR
            failed_at >= picked_up_at
        ),

    -- Only one terminal timestamp may be set at a time
    CONSTRAINT chk_single_terminal_timestamp
        CHECK (
            (
                CASE WHEN delivered_at  IS NOT NULL THEN 1 ELSE 0 END +
                CASE WHEN cancelled_at  IS NOT NULL THEN 1 ELSE 0 END +
                CASE WHEN failed_at     IS NOT NULL THEN 1 ELSE 0 END
            ) <= 1
        )
);

COMMENT ON TABLE  shipments                  IS 'Delivery contract. Addresses in shipment_addresses; OTP state in shipment_otp_windows.';
COMMENT ON COLUMN shipments.tracking_number  IS 'Public identifier. Format: TRK-XXXXXX. Generated by TrackingNumberService.';
COMMENT ON COLUMN shipments.customer_id      IS 'The Sender. NOT NULL — a shipment always has an owner.';
COMMENT ON COLUMN shipments.driver_id        IS 'NULL until a driver self-assigns. ON DELETE SET NULL preserves history.';
COMMENT ON COLUMN shipments.picked_up_at     IS 'Set when Pickup OTP is verified. Legal proof-of-pickup timestamp (POP).';
COMMENT ON COLUMN shipments.delivered_at     IS 'Set when Delivery OTP is verified. Legal proof-of-delivery timestamp (POD).';
COMMENT ON COLUMN shipments.cancelled_at     IS 'Set when status transitions to Cancelled. NULL for all other statuses.';
COMMENT ON COLUMN shipments.failed_at        IS 'Set when status transitions to FailedDelivery. NULL for all other statuses.';


-- ────────────────────────────────────────────────────────────────
--  3.7  shipment_addresses
--
--  One row per address point per shipment.
--  UNIQUE (shipment_id, address_type) enforces exactly one
--  Pickup and one Dropoff per shipment at the database level.
--
--  contact_name and contact_phone are only on the Dropoff row —
--  they identify the recipient. The sender is already identified
--  via customer_id → users on the shipments table.
-- ────────────────────────────────────────────────────────────────
CREATE TABLE shipment_addresses (
    id              SERIAL              PRIMARY KEY,
    shipment_id     INT                 NOT NULL,
    address_type    address_type        NOT NULL,
    address_line    TEXT                NOT NULL,
    lat             DOUBLE PRECISION    NULL,
    lng             DOUBLE PRECISION    NULL,
    contact_name    VARCHAR(100)        NULL,
    contact_phone   VARCHAR(20)         NULL,

    -- ── Uniqueness ────────────────────────────────────────────
    CONSTRAINT uq_shipment_addresses_type
        UNIQUE (shipment_id, address_type),

    -- ── Foreign keys ──────────────────────────────────────────
    CONSTRAINT fk_shipment_addresses_shipment
        FOREIGN KEY (shipment_id)
        REFERENCES shipments (id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    -- ── Business rules ────────────────────────────────────────
    CONSTRAINT chk_address_line_not_blank
        CHECK (LENGTH(TRIM(address_line)) > 0),

    -- Coordinates: both set or both null — never one without the other
    CONSTRAINT chk_coords_both_or_neither
        CHECK (
            (lat IS NULL AND lng IS NULL) OR
            (lat IS NOT NULL AND lng IS NOT NULL)
        ),

    CONSTRAINT chk_lat_range
        CHECK (lat IS NULL OR lat BETWEEN -90 AND 90),

    CONSTRAINT chk_lng_range
        CHECK (lng IS NULL OR lng BETWEEN -180 AND 180),

    -- Contact fields: both set or both null
    CONSTRAINT chk_contact_both_or_neither
        CHECK (
            (contact_name IS NULL AND contact_phone IS NULL) OR
            (contact_name IS NOT NULL AND contact_phone IS NOT NULL)
        ),

    -- Pickup rows must NOT carry contact details
    -- (sender identity comes from customer_id on shipments)
    CONSTRAINT chk_pickup_no_contact
        CHECK (
            address_type <> 'Pickup' OR
            (contact_name IS NULL AND contact_phone IS NULL)
        ),

    -- Dropoff rows MUST have contact details
    CONSTRAINT chk_dropoff_requires_contact
        CHECK (
            address_type <> 'Dropoff' OR
            (contact_name IS NOT NULL AND contact_phone IS NOT NULL)
        )
);

COMMENT ON TABLE  shipment_addresses               IS 'Address rows per shipment. Replaces 8 pickup/dropoff/recipient columns on shipments.';
COMMENT ON COLUMN shipment_addresses.address_type  IS 'Pickup = collection point. Dropoff = delivery destination.';
COMMENT ON COLUMN shipment_addresses.contact_name  IS 'NULL on Pickup (sender identified via customer_id). Required on Dropoff.';
COMMENT ON COLUMN shipment_addresses.contact_phone IS 'NULL on Pickup. Required on Dropoff — driver contacts recipient on arrival.';


-- ────────────────────────────────────────────────────────────────
--  3.8  shipment_items
--
--  1NF child table. Each physical item type in a booking
--  gets its own row. A booking with 3 different items has
--  3 rows here.
--
--  Total shipment weight = SUM(weight_kg * quantity) across
--  all rows for a given shipment_id.
-- ────────────────────────────────────────────────────────────────
CREATE TABLE shipment_items (
    id          SERIAL          PRIMARY KEY,
    shipment_id INT             NOT NULL,
    description VARCHAR(200)    NOT NULL,
    weight_kg   DECIMAL(8, 3)   NOT NULL,
    length_cm   DECIMAL(6, 1)   NOT NULL,
    width_cm    DECIMAL(6, 1)   NOT NULL,
    height_cm   DECIMAL(6, 1)   NOT NULL,
    quantity    SMALLINT        NOT NULL    DEFAULT 1,

    CONSTRAINT fk_shipment_items_shipment
        FOREIGN KEY (shipment_id)
        REFERENCES shipments (id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT chk_item_description_not_blank
        CHECK (LENGTH(TRIM(description)) > 0),

    CONSTRAINT chk_item_weight_positive
        CHECK (weight_kg > 0),

    CONSTRAINT chk_item_length_positive
        CHECK (length_cm > 0),

    CONSTRAINT chk_item_width_positive
        CHECK (width_cm > 0),

    CONSTRAINT chk_item_height_positive
        CHECK (height_cm > 0),

    CONSTRAINT chk_item_quantity_positive
        CHECK (quantity >= 1)
);

COMMENT ON TABLE  shipment_items           IS '1NF child table. One row per distinct item type per booking.';
COMMENT ON COLUMN shipment_items.weight_kg IS 'Weight per unit. Total shipment weight = SUM(weight_kg * quantity).';
COMMENT ON COLUMN shipment_items.quantity  IS 'Count of identical units of this item in the shipment.';


-- ────────────────────────────────────────────────────────────────
--  3.9  shipment_otp_windows
--
--  Extracted from shipments. Replaces 6 OTP columns.
--  One row per OTP type per shipment (Pickup + Delivery = 2 rows).
--
--  The row is inserted when the driver first calls request-otp
--  and upserted on regeneration. It is never deleted — it becomes
--  a permanent audit record with verified_at set on success and
--  otp_code cleared (NULL) for security.
--
--  otp_code is NULL in three situations:
--    1. No OTP has been requested yet (generated_at also NULL).
--    2. The window expired without verification.
--    3. Verification succeeded — code is cleared immediately.
-- ────────────────────────────────────────────────────────────────
CREATE TABLE shipment_otp_windows (
    id              SERIAL          PRIMARY KEY,
    shipment_id     INT             NOT NULL,
    otp_type        otp_type        NOT NULL,
    otp_code        CHAR(4)         NULL,
    expires_at      TIMESTAMPTZ     NULL,
    attempt_count   SMALLINT        NOT NULL    DEFAULT 0,
    generated_at    TIMESTAMPTZ     NULL,
    verified_at     TIMESTAMPTZ     NULL,

    -- ── Uniqueness ────────────────────────────────────────────
    CONSTRAINT uq_shipment_otp_type
        UNIQUE (shipment_id, otp_type),

    -- ── Foreign keys ──────────────────────────────────────────
    CONSTRAINT fk_shipment_otp_shipment
        FOREIGN KEY (shipment_id)
        REFERENCES shipments (id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    -- ── Business rules ────────────────────────────────────────

    -- Code and expiry: both set or both null
    CONSTRAINT chk_otp_code_expiry_consistency
        CHECK (
            (otp_code IS NULL AND expires_at IS NULL) OR
            (otp_code IS NOT NULL AND expires_at IS NOT NULL)
        ),

    -- OTP code must be exactly 4 digits when present
    CONSTRAINT chk_otp_code_format
        CHECK (otp_code IS NULL OR otp_code ~ '^[0-9]{4}$'),

    -- Attempt count: non-negative, hard cap at 3
    CONSTRAINT chk_otp_attempt_range
        CHECK (attempt_count BETWEEN 0 AND 3),

    -- verified_at must come after generated_at when both are set
    CONSTRAINT chk_verified_after_generated
        CHECK (
            verified_at IS NULL OR
            generated_at IS NULL OR
            verified_at >= generated_at
        ),

    -- On verification, otp_code is cleared (set NULL).
    -- If verified_at is set, otp_code must be NULL.
    CONSTRAINT chk_code_cleared_on_verification
        CHECK (verified_at IS NULL OR otp_code IS NULL)
);

COMMENT ON TABLE  shipment_otp_windows               IS 'OTP state rows. Replaces 6 OTP columns on shipments. One row per type per shipment.';
COMMENT ON COLUMN shipment_otp_windows.otp_type      IS 'Pickup = POP (proof of pickup). Delivery = POD (proof of delivery).';
COMMENT ON COLUMN shipment_otp_windows.otp_code      IS 'NULL when no active window or after successful verification. Never exposed in views.';
COMMENT ON COLUMN shipment_otp_windows.attempt_count IS 'Increments on wrong code. Reset to 0 on regeneration. Hard cap: 3.';
COMMENT ON COLUMN shipment_otp_windows.verified_at   IS 'Set on success. Never updated after. Permanent proof-of-verification record.';
COMMENT ON COLUMN shipment_otp_windows.generated_at  IS 'Audit: when the current code was issued or last regenerated.';


-- ────────────────────────────────────────────────────────────────
--  3.10  shipment_events
--
--  Append-only audit log. One row per status transition or
--  significant system event. Never UPDATE or DELETE in normal
--  operation. Every status change on shipments inserts one row
--  here in the SAME TRANSACTION — partial writes are unacceptable.
--
--  Covers all 8 statuses including Cancelled and FailedDelivery.
--  Each has its own event row with a human-readable description.
--
--  actor_id = NULL means the event was triggered by the
--  BackgroundService (GPS simulation) or another system process.
-- ────────────────────────────────────────────────────────────────
CREATE TABLE shipment_events (
    id          SERIAL              PRIMARY KEY,
    shipment_id INT                 NOT NULL,
    status      shipment_status     NOT NULL,
    description TEXT                NOT NULL,
    latitude    DOUBLE PRECISION    NULL,
    longitude   DOUBLE PRECISION    NULL,
    actor_id    INT                 NULL,
    occurred_at TIMESTAMPTZ         NOT NULL    DEFAULT NOW(),

    CONSTRAINT fk_shipment_events_shipment
        FOREIGN KEY (shipment_id)
        REFERENCES shipments (id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_shipment_events_actor
        FOREIGN KEY (actor_id)
        REFERENCES users (id)
        ON DELETE SET NULL
        ON UPDATE CASCADE,

    CONSTRAINT chk_event_description_not_blank
        CHECK (LENGTH(TRIM(description)) > 0),

    CONSTRAINT chk_event_lat_range
        CHECK (latitude  IS NULL OR latitude  BETWEEN -90  AND 90),

    CONSTRAINT chk_event_lng_range
        CHECK (longitude IS NULL OR longitude BETWEEN -180 AND 180)
);

COMMENT ON TABLE  shipment_events             IS 'Append-only audit log. Insert once per transition. Never update or delete rows.';
COMMENT ON COLUMN shipment_events.status      IS 'Status at the moment of the event. Matches shipments.status after the transition.';
COMMENT ON COLUMN shipment_events.actor_id    IS 'NULL = system/BackgroundService. Set to user ID for Driver, Customer, and Admin actions.';
COMMENT ON COLUMN shipment_events.latitude    IS 'GPS snapshot at event time. Accumulates the breadcrumb trail.';
COMMENT ON COLUMN shipment_events.occurred_at IS 'Always ORDER BY occurred_at ASC for the tracking timeline.';


-- ================================================================
--  SECTION 4 — INDEXES
--
--  Capstone-appropriate set: 4 implicit UNIQUE indexes +
--  9 explicit indexes covering all critical FK joins and
--  the most common filter columns.
--
--  Dropped from v4.0:
--    • idx_refresh_tokens_user_id (refresh_tokens table dropped)
--
--  Dropped from v3.0 (carried forward):
--    • All partial indexes (production optimisation only)
--    • All composite indexes (overkill at capstone data volumes)
--
--  Total: ~13 indexes
--    4  implicit from UNIQUE constraints
--      (uq_users_email, uq_shipments_tracking_number,
--       uq_driver_profiles_user_id, uq_customer_profiles_user_id)
--    9 explicit (listed below)
-- ================================================================

-- ── users ────────────────────────────────────────────────────────
CREATE INDEX idx_users_role
    ON users (role);
-- Admin user-listing queries filter by role.


-- ── saved_addresses ───────────────────────────────────────────────
CREATE INDEX idx_saved_addresses_customer_id
    ON saved_addresses (customer_id);
-- FK join: load address book for a customer.

CREATE UNIQUE INDEX uix_saved_addresses_one_default_per_customer
    ON saved_addresses (customer_id)
    WHERE is_default = TRUE;
-- Data integrity: enforces at most one default address per customer.
-- Non-default rows are not covered — many are allowed.


-- ── driver_profiles ──────────────────────────────────────────────
CREATE INDEX idx_driver_profiles_account_status
    ON driver_profiles (account_status);
-- Admin approval queue filters by account_status = 'PendingApproval'.


-- ── shipments ─────────────────────────────────────────────────────
CREATE INDEX idx_shipments_customer_id
    ON shipments (customer_id);
-- "My Shipments" page — load all shipments for a customer.

CREATE INDEX idx_shipments_driver_id
    ON shipments (driver_id)
    WHERE driver_id IS NOT NULL;
-- Driver's active/past jobs — excludes unassigned (NULL driver_id).

CREATE INDEX idx_shipments_status
    ON shipments (status);
-- Job queue (Pending filter) + admin status filter.


-- ── shipment_addresses ────────────────────────────────────────────
CREATE INDEX idx_shipment_addresses_shipment_id
    ON shipment_addresses (shipment_id);
-- FK join: load pickup + dropoff for a shipment.


-- ── shipment_items ────────────────────────────────────────────────
CREATE INDEX idx_shipment_items_shipment_id
    ON shipment_items (shipment_id);
-- FK join: load item list for booking detail page.


-- ── shipment_otp_windows ─────────────────────────────────────────
CREATE INDEX idx_shipment_otp_shipment_id
    ON shipment_otp_windows (shipment_id);
-- FK join: load OTP windows for a shipment.


-- ── shipment_events ───────────────────────────────────────────────
CREATE INDEX idx_shipment_events_shipment_id
    ON shipment_events (shipment_id);
-- Tracking timeline — load all events for a shipment.


-- ================================================================
--  SECTION 5 — TRIGGERS
--
--  Automatically maintains updated_at on every UPDATE.
--  Prevents the application layer from ever forgetting to set it.
--
--  Kept: users + shipments (updated frequently in normal operation).
--  Dropped: customer_profiles + driver_profiles (rarely updated;
--           EF Core can handle updated_at in the service layer).
-- ================================================================

CREATE OR REPLACE FUNCTION fn_set_updated_at()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$;

COMMENT ON FUNCTION fn_set_updated_at()
    IS 'Sets updated_at = NOW() on every UPDATE. Attached as BEFORE UPDATE trigger on tables with high update frequency.';

CREATE TRIGGER trg_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW
    EXECUTE FUNCTION fn_set_updated_at();

CREATE TRIGGER trg_shipments_updated_at
    BEFORE UPDATE ON shipments
    FOR EACH ROW
    EXECUTE FUNCTION fn_set_updated_at();


-- ================================================================
--  SECTION 6 — SEED DATA
--
--  Admin account is pre-seeded. There is no self-registration
--  path for the Admin role — accounts are created at deployment.
--
--  IMPORTANT: Replace 'REPLACE_WITH_IDENTITY_HASH' with the
--  actual output of ASP.NET Core Identity PasswordHasher before
--  running in any real environment. Never commit a real hash
--  to version control.
-- ================================================================

INSERT INTO users (
    email,
    full_name,
    password_hash,
    role,
    is_active
)
VALUES (
    'admin@swiftparcel.com',
    'Platform Admin',
    'REPLACE_WITH_IDENTITY_HASH',
    'Admin',
    TRUE
)
ON CONFLICT (email) DO NOTHING;
-- ON CONFLICT: safe to re-run this script without duplicate errors.
-- Admin does not get a customer_profiles or driver_profiles row.


-- ================================================================
--  SECTION 7 — STATE MACHINE REFERENCE
--
--  Documented here for developers implementing the service layer.
--  The DB does not enforce transition order — that is the job of
--  ShipmentService.ValidateTransition() at the application layer.
--
--  Valid transitions:
--
--    FROM             TO                ACTOR       GUARD
--    ─────────────    ──────────────    ─────────   ────────────────────────────
--    (new)            Pending           Customer    Successful booking
--    Pending          Assigned          Driver      Active + Available; DB row lock
--    Assigned         PickedUp          Driver      Pickup OTP valid, <3 attempts
--    PickedUp         InTransit         Driver      Owns shipment in PickedUp state
--    InTransit        Arrived           Driver      Owns shipment in InTransit state
--    Arrived          Delivered         Driver      Delivery OTP valid, <3 attempts
--    Arrived          FailedDelivery    Driver      Cannot complete delivery
--    Pending          Cancelled         Customer    Status = Pending only
--    Any non-terminal Cancelled         Admin       Admin role; inserts override event
--    Any non-terminal FailedDelivery    Admin       Admin role; inserts override event
--    Any              Any               Admin       Override; always inserts event row
--
--  Terminal states (no further transitions except Admin override):
--    Delivered, Cancelled, FailedDelivery
--
--  Invalid transitions return HTTP 409 Conflict.
-- ================================================================


-- ================================================================
--  SECTION 8 — SCHEMA SUMMARY
-- ================================================================

--  Extensions (2):
--    pgcrypto, citext
--
--  Enum types (6):
--    user_role
--    driver_account_status
--    driver_op_status
--    shipment_status          (8 values incl. Cancelled, FailedDelivery)
--    address_type
--    otp_type
--
--  Tables (9):
--    users
--    customer_profiles
--    saved_addresses
--    driver_profiles
--    shipments
--    shipment_addresses
--    shipment_items
--    shipment_otp_windows
--    shipment_events
--
--  CHECK constraints: all retained from v3.0
--
--  Indexes (~13 total):
--    4  implicit from UNIQUE constraints
--       (uq_users_email, uq_shipments_tracking_number,
--        uq_driver_profiles_user_id, uq_customer_profiles_user_id)
--    9 explicit standard indexes (all FK joins + role/status filters)
--    1  partial unique (uix_saved_addresses_one_default_per_customer)
--
--  Triggers (2):
--    fn_set_updated_at()          shared function
--    trg_users_updated_at
--    trg_shipments_updated_at
--
--  Views: none (use EF Core .Include() / .Select() in repositories)
--
--  Seed data: admin user only
--
--  Normalisation level: 3NF / BCNF throughout
--
-- ================================================================
--  END OF SCHEMA — SwiftParcel v4.1 (capstone, JWT-only)
-- ================================================================
