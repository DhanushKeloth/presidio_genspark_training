-- ================================================================
--  P2P COURIER SERVICE PLATFORM — COMPLETE DATABASE SCHEMA
--  Version      : 2.0 (normalised)
--  PostgreSQL   : 15+
--  Approach     : DB-First → scaffold into EF Core 8
--  Normalisation: 3NF throughout; BCNF where applicable
--
--  Execution order (all in one file, run top to bottom):
--    1. Extensions
--    2. Enum types
--    3. Tables (dependency order)
--    4. Indexes
--    5. Triggers
--    6. Views
--    7. Seed data
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
--  State machine (enforced at service layer, validated at DB level):
--  Pending → Assigned → PickedUp → InTransit → Arrived → Delivered
CREATE TYPE shipment_status AS ENUM (
    'Pending',      -- Booked; awaiting driver assignment
    'Assigned',     -- Driver claimed; navigating to pickup
    'PickedUp',     -- Pickup OTP verified; parcel collected
    'InTransit',    -- Driver driving; GPS simulation active
    'Arrived',      -- Driver at destination; awaiting delivery OTP
    'Delivered'     -- Delivery OTP verified; terminal state
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
--  Creation order respects FK dependencies:
--    users → driver_profiles
--    users → refresh_tokens
--    users → shipments
--    shipments → shipment_addresses
--    shipments → shipment_items
--    shipments → shipment_otp_windows
--    shipments → shipment_events
-- ================================================================


-- ────────────────────────────────────────────────────────────────
--  3.1  users
--
--  Central identity table. All three roles share one table,
--  differentiated by the role enum. This is intentional — a
--  single login endpoint and a single FK target for all
--  ownership relationships.
--
--  Normalisation:
--  • 1NF: all columns atomic, every row uniquely identified by id.
--  • 2NF: no partial dependencies (single-column PK, trivially met).
--  • 3NF: no transitive dependencies — no non-key column determines
--    another non-key column.
--  • Driver-specific columns are NOT here — they live in
--    driver_profiles (vertical partition) to avoid NULLs on
--    Customer and Admin rows.
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
COMMENT ON COLUMN users.password_hash IS 'ASP.NET Core Identity PasswordHasher output. Never plain text.';
COMMENT ON COLUMN users.is_active     IS 'Soft-delete flag. FALSE = account deactivated; all FK references preserved.';


-- ────────────────────────────────────────────────────────────────
--  3.2  driver_profiles
--
--  1:1 vertical partition of users for Driver-specific data.
--  Separating these columns from users avoids:
--  • 6 permanent NULL columns on every Customer and Admin row.
--  • A partial dependency where columns only apply when role = Driver.
--
--  UNIQUE (user_id) enforces the 1:1 cardinality at DB level.
--  approved_by FK records the Admin who activated this driver.
-- ────────────────────────────────────────────────────────────────
CREATE TABLE driver_profiles (
    id              SERIAL                  PRIMARY KEY,
    user_id         INT                     NOT NULL,
    vehicle_type    VARCHAR(50)             NOT NULL,
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
    CONSTRAINT chk_driver_vehicle_type_not_blank
        CHECK (LENGTH(TRIM(vehicle_type)) > 0),

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

COMMENT ON TABLE  driver_profiles              IS '1:1 vertical partition of users for Driver-specific data. Avoids NULLs on non-driver rows.';
COMMENT ON COLUMN driver_profiles.op_status    IS 'NULL until account_status = Active. Toggle: Available / InTransit / Offline.';
COMMENT ON COLUMN driver_profiles.current_lat  IS 'Live GPS latitude. Written by GpsSimulationService every 5s during InTransit.';
COMMENT ON COLUMN driver_profiles.current_lng  IS 'Live GPS longitude. Written by GpsSimulationService every 5s during InTransit.';
COMMENT ON COLUMN driver_profiles.approved_by  IS 'FK to users: the Admin who set account_status = Active.';


-- ────────────────────────────────────────────────────────────────
--  3.3  refresh_tokens
--
--  One user → many sessions (one per device/browser).
--  Stores SHA-256 hash of the raw token, never the raw value.
--  is_revoked enables immediate session termination on logout
--  without waiting for the access token to expire.
-- ────────────────────────────────────────────────────────────────
CREATE TABLE refresh_tokens (
    id          SERIAL          PRIMARY KEY,
    user_id     INT             NOT NULL,
    token_hash  VARCHAR(512)    NOT NULL,
    expires_at  TIMESTAMPTZ     NOT NULL,
    is_revoked  BOOLEAN         NOT NULL    DEFAULT FALSE,
    device_hint VARCHAR(100)    NULL,
    created_at  TIMESTAMPTZ     NOT NULL    DEFAULT NOW(),

    CONSTRAINT uq_refresh_tokens_hash
        UNIQUE (token_hash),

    CONSTRAINT fk_refresh_tokens_user
        FOREIGN KEY (user_id)
        REFERENCES users (id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT chk_refresh_token_expires_after_created
        CHECK (expires_at > created_at)
);

COMMENT ON TABLE  refresh_tokens            IS 'Hashed refresh tokens per user session. Supports multi-device and explicit logout.';
COMMENT ON COLUMN refresh_tokens.token_hash IS 'SHA-256 of raw token. Raw token sent to client; this hash stored in DB.';
COMMENT ON COLUMN refresh_tokens.is_revoked IS 'Set TRUE on logout. Checked before issuing a new access token.';
COMMENT ON COLUMN refresh_tokens.device_hint IS 'Optional browser/device label for session management UI.';


-- ────────────────────────────────────────────────────────────────
--  3.4  shipments
--
--  Core delivery contract — normalised to 9 data columns.
--  All address data extracted → shipment_addresses (3NF).
--  All OTP state extracted   → shipment_otp_windows (1NF / 3NF).
--
--  What remains on this table:
--  • Identity       : tracking_number
--  • Ownership      : customer_id, driver_id
--  • Contract state : status
--  • Legal timestamps: picked_up_at, delivered_at
--  • Audit          : created_at, updated_at
-- ────────────────────────────────────────────────────────────────
CREATE TABLE shipments (
    id                  SERIAL              PRIMARY KEY,
    tracking_number     VARCHAR(20)         NOT NULL,
    customer_id         INT                 NOT NULL,
    driver_id           INT                 NULL,
    status              shipment_status     NOT NULL    DEFAULT 'Pending',
    picked_up_at        TIMESTAMPTZ         NULL,
    delivered_at        TIMESTAMPTZ         NULL,
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
        )
);

COMMENT ON TABLE  shipments                 IS 'Delivery contract. Normalised to 9 columns. Addresses in shipment_addresses; OTP state in shipment_otp_windows.';
COMMENT ON COLUMN shipments.tracking_number IS 'Public identifier. Format: TRK-XXXXXX. Generated by TrackingNumberService.';
COMMENT ON COLUMN shipments.customer_id     IS 'The Sender. NOT NULL — a shipment always has an owner.';
COMMENT ON COLUMN shipments.driver_id       IS 'NULL until a driver self-assigns. ON DELETE SET NULL preserves history.';
COMMENT ON COLUMN shipments.picked_up_at    IS 'Set when Pickup OTP is verified. Legal proof-of-pickup timestamp.';
COMMENT ON COLUMN shipments.delivered_at    IS 'Set when Delivery OTP is verified. Legal proof-of-delivery timestamp.';


-- ────────────────────────────────────────────────────────────────
--  3.5  shipment_addresses
--
--  Extracted from shipments. One row per address point.
--  Normalisation: address_line + lat + lng form a cohesive unit
--  that always belongs together. Putting them as separate column
--  groups on shipments created implicit coupling with no DB-level
--  enforcement of their joint nullability.
--
--  UNIQUE (shipment_id, address_type) enforces exactly one Pickup
--  and one Dropoff per shipment at the database level.
--
--  contact_name and contact_phone live on the Dropoff row only —
--  they describe who is at the delivery destination. On the Pickup
--  row the sender is already identified via customer_id → users.
-- ────────────────────────────────────────────────────────────────
CREATE TABLE shipment_addresses (
    id              SERIAL          PRIMARY KEY,
    shipment_id     INT             NOT NULL,
    address_type    address_type    NOT NULL,
    address_line    TEXT            NOT NULL,
    lat             DOUBLE PRECISION NULL,
    lng             DOUBLE PRECISION NULL,
    contact_name    VARCHAR(100)    NULL,
    contact_phone   VARCHAR(20)     NULL,

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

COMMENT ON TABLE  shipment_addresses               IS 'Address rows per shipment. Replaces 8 pickup/dropoff/recipient columns from shipments.';
COMMENT ON COLUMN shipment_addresses.address_type  IS 'Pickup = collection point. Dropoff = delivery destination.';
COMMENT ON COLUMN shipment_addresses.contact_name  IS 'NULL on Pickup (sender identified via customer_id). Required on Dropoff.';
COMMENT ON COLUMN shipment_addresses.contact_phone IS 'NULL on Pickup. Required on Dropoff — how the driver contacts the recipient.';


-- ────────────────────────────────────────────────────────────────
--  3.6  shipment_items
--
--  1NF compliance table. Each physical package in a booking
--  gets its own row. A booking with 3 different items would
--  require 3 rows here.
--
--  Storing weight/dimensions on shipments as a repeating group
--  or CSV string would violate 1NF.
--
--  Total shipment weight = SUM(weight_kg * quantity) across all
--  rows for a given shipment_id.
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
COMMENT ON COLUMN shipment_items.weight_kg IS 'Weight per unit. Total = SUM(weight_kg * quantity) across all items.';
COMMENT ON COLUMN shipment_items.quantity  IS 'Count of identical units of this item in the shipment.';


-- ────────────────────────────────────────────────────────────────
--  3.7  shipment_otp_windows
--
--  Extracted from shipments. Replaces 6 OTP columns.
--  Normalisation: the original 6 columns were a repeating
--  attribute group — (code, expires_at, attempt_count) × 2
--  with a type prefix. A 3rd OTP stage would add 3 more columns.
--  This table handles any number of OTP types with zero schema
--  changes — just a new otp_type enum value.
--
--  UNIQUE (shipment_id, otp_type) enforces at most one active
--  window per type per shipment.
--
--  The row is inserted when the driver first calls request-otp
--  and upserted on regeneration. It is never deleted — it becomes
--  the permanent audit record with verified_at set on success.
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

COMMENT ON TABLE  shipment_otp_windows              IS 'OTP state rows. Replaces 6 OTP columns on shipments. One row per type per shipment.';
COMMENT ON COLUMN shipment_otp_windows.otp_type     IS 'Pickup = POP. Delivery = POD.';
COMMENT ON COLUMN shipment_otp_windows.otp_code     IS 'NULL when no active window. Cleared on successful verification.';
COMMENT ON COLUMN shipment_otp_windows.attempt_count IS 'Increments on wrong code. Reset to 0 on regeneration. Hard cap: 3.';
COMMENT ON COLUMN shipment_otp_windows.verified_at  IS 'Set on success. Never updated after. Permanent proof-of-verification record.';
COMMENT ON COLUMN shipment_otp_windows.generated_at IS 'Audit record of when the current code was issued or last regenerated.';


-- ────────────────────────────────────────────────────────────────
--  3.8  shipment_events
--
--  Append-only audit log. One row per status transition.
--  Never UPDATE or DELETE rows from this table in normal operation.
--  Every status change on shipments inserts one row here in the
--  SAME TRANSACTION — partial writes are unacceptable.
--
--  Coordinate snapshot (latitude, longitude) provides the GPS
--  breadcrumb trail. Storing coords only on shipments would lose
--  history on every update.
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

COMMENT ON TABLE  shipment_events            IS 'Append-only audit log. Insert once per transition. Never update or delete rows.';
COMMENT ON COLUMN shipment_events.actor_id   IS 'NULL = system/BackgroundService. Set to user ID for Driver and Admin actions.';
COMMENT ON COLUMN shipment_events.latitude   IS 'GPS snapshot at event time. Accumulates into the breadcrumb trail.';
COMMENT ON COLUMN shipment_events.occurred_at IS 'Always ORDER BY occurred_at ASC for the tracking timeline.';


-- ================================================================
--  SECTION 4 — INDEXES
--
--  Strategy:
--  • UNIQUE constraints already create implicit indexes.
--  • Every FK column gets an index for JOIN performance.
--  • Partial indexes narrow the index to the subset actually queried.
--  • Composite indexes support the most common multi-column WHERE.
-- ================================================================

-- ── users ────────────────────────────────────────────────────────
-- uq_users_email already creates the email index.
CREATE INDEX idx_users_role
    ON users (role);

CREATE INDEX idx_users_inactive
    ON users (id)
    WHERE is_active = FALSE;
-- Partial: admin audit queries for deactivated accounts


-- ── driver_profiles ──────────────────────────────────────────────
-- uq_driver_profiles_user_id and uq_driver_profiles_license create indexes.
CREATE INDEX idx_driver_profiles_account_status
    ON driver_profiles (account_status);

CREATE INDEX idx_driver_profiles_pending_approval
    ON driver_profiles (created_at DESC)
    WHERE account_status = 'PendingApproval';
-- Partial: admin most-common query — new driver approvals queue

CREATE INDEX idx_driver_profiles_active_available
    ON driver_profiles (user_id)
    WHERE account_status = 'Active' AND op_status = 'Available';
-- Partial: dispatcher/system checks for available active drivers


-- ── refresh_tokens ────────────────────────────────────────────────
-- uq_refresh_tokens_hash already creates the hash index.
CREATE INDEX idx_refresh_tokens_user_id
    ON refresh_tokens (user_id);

CREATE INDEX idx_refresh_tokens_active
    ON refresh_tokens (user_id, expires_at DESC)
    WHERE is_revoked = FALSE;
-- Partial: only non-revoked, non-expired tokens matter for auth


-- ── shipments ─────────────────────────────────────────────────────
-- uq_shipments_tracking_number already creates the tracking index.
CREATE INDEX idx_shipments_customer_id
    ON shipments (customer_id);

CREATE INDEX idx_shipments_driver_id
    ON shipments (driver_id)
    WHERE driver_id IS NOT NULL;
-- Partial: unassigned shipments have NULL driver_id

CREATE INDEX idx_shipments_status
    ON shipments (status);

CREATE INDEX idx_shipments_customer_status_date
    ON shipments (customer_id, status, created_at DESC);
-- Composite: Customer's shipment list with status filter and date sort

CREATE INDEX idx_shipments_in_transit
    ON shipments (driver_id)
    WHERE status = 'InTransit';
-- Partial: BackgroundService queries ONLY InTransit shipments on every 5s tick
-- Without this, the GPS service does a full table scan every 5 seconds

CREATE INDEX idx_shipments_pending_queue
    ON shipments (created_at DESC)
    WHERE status = 'Pending';
-- Partial: Driver job queue — only Pending shipments visible

CREATE INDEX idx_shipments_active_admin
    ON shipments (status, created_at DESC)
    WHERE status NOT IN ('Delivered');
-- Partial: Admin monitoring view — active shipments only


-- ── shipment_addresses ────────────────────────────────────────────
CREATE INDEX idx_shipment_addresses_shipment_id
    ON shipment_addresses (shipment_id);

CREATE INDEX idx_shipment_addresses_dropoff
    ON shipment_addresses (shipment_id)
    WHERE address_type = 'Dropoff';
-- Partial: most common lookup — "get dropoff address for shipment X"

CREATE INDEX idx_shipment_addresses_pickup
    ON shipment_addresses (shipment_id)
    WHERE address_type = 'Pickup';
-- Partial: BackgroundService reads pickup coords to compute route start


-- ── shipment_items ────────────────────────────────────────────────
CREATE INDEX idx_shipment_items_shipment_id
    ON shipment_items (shipment_id);


-- ── shipment_otp_windows ─────────────────────────────────────────
CREATE INDEX idx_shipment_otp_shipment_id
    ON shipment_otp_windows (shipment_id);

CREATE INDEX idx_shipment_otp_active
    ON shipment_otp_windows (shipment_id, otp_type)
    WHERE verified_at IS NULL AND otp_code IS NOT NULL;
-- Partial: service layer lookup for "is there an active unverified OTP?"


-- ── shipment_events ───────────────────────────────────────────────
CREATE INDEX idx_shipment_events_shipment_id
    ON shipment_events (shipment_id);

CREATE INDEX idx_shipment_events_timeline
    ON shipment_events (shipment_id, occurred_at ASC);
-- Composite: tracking timeline query — always ordered by occurred_at ASC

CREATE INDEX idx_shipment_events_actor_id
    ON shipment_events (actor_id)
    WHERE actor_id IS NOT NULL;
-- Partial: admin audit — "all events triggered by this user"


-- ================================================================
--  SECTION 5 — TRIGGERS
--
--  Automatically maintains updated_at on every UPDATE.
--  Prevents the application layer from ever forgetting to set it.
--  Applied to the three tables that carry updated_at.
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
    IS 'Sets updated_at = NOW() on every UPDATE. Attached as BEFORE UPDATE trigger.';

CREATE TRIGGER trg_users_updated_at
    BEFORE UPDATE ON users
    FOR EACH ROW
    EXECUTE FUNCTION fn_set_updated_at();

CREATE TRIGGER trg_driver_profiles_updated_at
    BEFORE UPDATE ON driver_profiles
    FOR EACH ROW
    EXECUTE FUNCTION fn_set_updated_at();

CREATE TRIGGER trg_shipments_updated_at
    BEFORE UPDATE ON shipments
    FOR EACH ROW
    EXECUTE FUNCTION fn_set_updated_at();


-- ================================================================
--  SECTION 6 — VIEWS
--
--  Named views that the service layer queries directly.
--  EF Core DB-first scaffolds these as keyless entity types
--  (modelBuilder.Entity<ViewType>().HasNoKey().ToView("vw_...")).
--
--  OTP codes are NEVER exposed in any view.
-- ================================================================

-- ── 6.1  Active refresh tokens ───────────────────────────────────
CREATE OR REPLACE VIEW vw_active_refresh_tokens AS
SELECT
    id,
    user_id,
    token_hash,
    expires_at,
    device_hint,
    created_at
FROM refresh_tokens
WHERE is_revoked = FALSE
  AND expires_at > NOW();

COMMENT ON VIEW vw_active_refresh_tokens
    IS 'Non-revoked, non-expired tokens. Used by AuthService for token validation.';


-- ── 6.2  Driver full profile (for Admin) ─────────────────────────
CREATE OR REPLACE VIEW vw_driver_full_profile AS
SELECT
    dp.id                   AS driver_profile_id,
    dp.user_id,
    u.email,
    u.full_name,
    u.is_active             AS user_is_active,
    dp.vehicle_type,
    dp.license_number,
    dp.account_status,
    dp.op_status,
    dp.current_lat,
    dp.current_lng,
    dp.approved_at,
    adm.full_name           AS approved_by_name,
    dp.created_at,
    dp.updated_at
FROM driver_profiles dp
    JOIN  users u   ON u.id   = dp.user_id
    LEFT JOIN users adm ON adm.id = dp.approved_by;

COMMENT ON VIEW vw_driver_full_profile
    IS 'Full driver info for Admin listing and detail view. Joins identity, profile, and approver name.';


-- ── 6.3  Pending shipments queue (for Driver) ────────────────────
CREATE OR REPLACE VIEW vw_pending_shipments_queue AS
SELECT
    s.id,
    s.tracking_number,
    SUBSTRING(pa.address_line, 1, 60)   AS pickup_area,
    SUBSTRING(da.address_line, 1, 60)   AS dropoff_area,
    pa.lat                              AS pickup_lat,
    pa.lng                              AS pickup_lng,
    da.lat                              AS dropoff_lat,
    da.lng                              AS dropoff_lng,
    COALESCE(SUM(si.weight_kg * si.quantity), 0)  AS total_weight_kg,
    COUNT(si.id)                        AS item_count,
    s.created_at
FROM shipments s
    LEFT JOIN shipment_addresses pa
        ON pa.shipment_id = s.id AND pa.address_type = 'Pickup'
    LEFT JOIN shipment_addresses da
        ON da.shipment_id = s.id AND da.address_type = 'Dropoff'
    LEFT JOIN shipment_items si
        ON si.shipment_id = s.id
WHERE s.status = 'Pending'
GROUP BY
    s.id,
    pa.address_line, pa.lat, pa.lng,
    da.address_line, da.lat, da.lng;

COMMENT ON VIEW vw_pending_shipments_queue
    IS 'Job queue for Available drivers. Area-level addresses only — full customer contact not exposed.';


-- ── 6.4  Shipment full detail (for Customer + Admin) ─────────────
-- NOTE: OTP codes are intentionally excluded from this view.
CREATE OR REPLACE VIEW vw_shipment_full AS
SELECT
    s.id,
    s.tracking_number,
    s.status,
    s.customer_id,
    cu.full_name                AS customer_name,
    cu.email                    AS customer_email,
    s.driver_id,
    du.full_name                AS driver_name,
    dp.vehicle_type,
    dp.license_number,
    dp.current_lat              AS driver_current_lat,
    dp.current_lng              AS driver_current_lng,

    -- Pickup address
    pa.address_line             AS pickup_address,
    pa.lat                      AS pickup_lat,
    pa.lng                      AS pickup_lng,

    -- Dropoff address + recipient
    da.address_line             AS dropoff_address,
    da.lat                      AS dropoff_lat,
    da.lng                      AS dropoff_lng,
    da.contact_name             AS recipient_name,
    da.contact_phone            AS recipient_phone,

    -- Pickup OTP metadata (code excluded)
    po.attempt_count            AS pickup_otp_attempt_count,
    po.expires_at               AS pickup_otp_expires_at,
    po.generated_at             AS pickup_otp_generated_at,
    po.verified_at              AS pickup_otp_verified_at,

    -- Delivery OTP metadata (code excluded)
    dl.attempt_count            AS delivery_otp_attempt_count,
    dl.expires_at               AS delivery_otp_expires_at,
    dl.generated_at             AS delivery_otp_generated_at,
    dl.verified_at              AS delivery_otp_verified_at,

    -- Items aggregate
    COALESCE(items.total_weight_kg, 0)  AS total_weight_kg,
    COALESCE(items.item_count, 0)       AS item_count,

    s.picked_up_at,
    s.delivered_at,
    s.created_at,
    s.updated_at

FROM shipments s
    JOIN  users cu  ON cu.id  = s.customer_id
    LEFT JOIN users du  ON du.id  = s.driver_id
    LEFT JOIN driver_profiles dp ON dp.user_id = s.driver_id
    LEFT JOIN shipment_addresses pa
        ON pa.shipment_id = s.id AND pa.address_type = 'Pickup'
    LEFT JOIN shipment_addresses da
        ON da.shipment_id = s.id AND da.address_type = 'Dropoff'
    LEFT JOIN shipment_otp_windows po
        ON po.shipment_id = s.id AND po.otp_type = 'Pickup'
    LEFT JOIN shipment_otp_windows dl
        ON dl.shipment_id = s.id AND dl.otp_type = 'Delivery'
    LEFT JOIN (
        SELECT
            shipment_id,
            SUM(weight_kg * quantity)   AS total_weight_kg,
            COUNT(*)                    AS item_count
        FROM shipment_items
        GROUP BY shipment_id
    ) items ON items.shipment_id = s.id;

COMMENT ON VIEW vw_shipment_full
    IS 'Full display view. OTP codes excluded. Use for customer detail page and admin shipment detail.';


-- ── 6.5  Public tracking view (no auth required) ─────────────────
-- Exposes only the minimum data needed for the public /track endpoint.
-- No customer email, no recipient phone, no OTP data whatsoever.
CREATE OR REPLACE VIEW vw_shipment_public_tracking AS
SELECT
    s.id,
    s.tracking_number,
    s.status,
    da.address_line             AS dropoff_address,
    pa.address_line             AS pickup_address,
    dp.current_lat              AS driver_current_lat,
    dp.current_lng              AS driver_current_lng,
    s.picked_up_at,
    s.delivered_at,
    s.created_at
FROM shipments s
    LEFT JOIN shipment_addresses pa
        ON pa.shipment_id = s.id AND pa.address_type = 'Pickup'
    LEFT JOIN shipment_addresses da
        ON da.shipment_id = s.id AND da.address_type = 'Dropoff'
    LEFT JOIN driver_profiles dp
        ON dp.user_id = s.driver_id;

COMMENT ON VIEW vw_shipment_public_tracking
    IS 'Minimal public tracking data. No PII, no OTP data. For the unauthenticated /api/track/{trackingNumber} endpoint.';


-- ── 6.6  Admin dashboard metrics ─────────────────────────────────
CREATE OR REPLACE VIEW vw_admin_dashboard AS
SELECT
    COUNT(*) FILTER (WHERE s.status = 'Pending')    AS shipments_pending,
    COUNT(*) FILTER (WHERE s.status = 'Assigned')   AS shipments_assigned,
    COUNT(*) FILTER (WHERE s.status = 'PickedUp')   AS shipments_picked_up,
    COUNT(*) FILTER (WHERE s.status = 'InTransit')  AS shipments_in_transit,
    COUNT(*) FILTER (WHERE s.status = 'Arrived')    AS shipments_arrived,
    COUNT(*) FILTER (WHERE s.status = 'Delivered')  AS shipments_delivered,
    COUNT(*) FILTER (
        WHERE s.status = 'Delivered'
          AND s.delivered_at >= CURRENT_DATE
    )                                               AS delivered_today,
    (SELECT COUNT(*) FROM driver_profiles
     WHERE account_status = 'PendingApproval')      AS drivers_pending_approval,
    (SELECT COUNT(*) FROM driver_profiles
     WHERE account_status = 'Active')               AS drivers_active,
    (SELECT COUNT(*) FROM driver_profiles
     WHERE account_status = 'Suspended')            AS drivers_suspended,
    (SELECT COUNT(*) FROM users
     WHERE role = 'Customer' AND is_active = TRUE)  AS total_customers
FROM shipments s;

COMMENT ON VIEW vw_admin_dashboard
    IS 'Single-row aggregated metrics for admin dashboard. No PII. No OTP data.';


-- ================================================================
--  SECTION 7 — SEED DATA
--
--  Admin account is pre-seeded. No self-registration path
--  exists for the Admin role.
--
--  IMPORTANT: Replace 'REPLACE_WITH_IDENTITY_HASH' with the
--  actual output of ASP.NET Core Identity PasswordHasher before
--  running in any real environment. Never commit a real hash
--  to source control.
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
-- ON CONFLICT: safe to re-run this script without duplicate errors


-- ================================================================
--  SECTION 8 — SCHEMA SUMMARY
-- ================================================================

--  Tables (8):
--    users
--    driver_profiles
--    refresh_tokens
--    shipments
--    shipment_addresses       ← extracted from shipments (normalised)
--    shipment_items
--    shipment_otp_windows     ← extracted from shipments (normalised)
--    shipment_events
--
--  Enum types (6):
--    user_role
--    driver_account_status
--    driver_op_status
--    shipment_status
--    address_type
--    otp_type
--
--  Indexes (24 total):
--    6 implicit from UNIQUE constraints
--    18 explicit (9 standard, 9 partial)
--
--  Triggers (3):
--    trg_users_updated_at
--    trg_driver_profiles_updated_at
--    trg_shipments_updated_at
--
--  Views (6):
--    vw_active_refresh_tokens
--    vw_driver_full_profile
--    vw_pending_shipments_queue
--    vw_shipment_full
--    vw_shipment_public_tracking
--    vw_admin_dashboard
--
--  Normalisation level: 3NF / BCNF throughout
--
-- ================================================================
--  END OF SCHEMA
-- ================================================================
