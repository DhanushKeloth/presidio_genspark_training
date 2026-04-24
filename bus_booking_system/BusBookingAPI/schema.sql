-- =============================================================================
-- BUS BOOKING SYSTEM — PostgreSQL Schema v1.0
-- Generated from: BusBooking_DatabaseDesign.docx
-- Target DB: PostgreSQL 16
-- =============================================================================

-- Enable UUID generation extension
CREATE EXTENSION IF NOT EXISTS pgcrypto;


-- =============================================================================
-- 1. ENUM TYPES
-- =============================================================================

CREATE TYPE operator_status      AS ENUM ('pending', 'approved', 'rejected', 'disabled');
CREATE TYPE bus_status           AS ENUM ('active', 'maintenance', 'removed');
CREATE TYPE booking_status       AS ENUM ('confirmed', 'cancelled', 'completed');
CREATE TYPE payment_status       AS ENUM ('pending', 'success', 'failed', 'refunded');
CREATE TYPE payment_method_enum  AS ENUM ('dummy', 'stripe_card', 'stripe_upi');
CREATE TYPE seat_type_enum       AS ENUM ('seater', 'lower_berth', 'upper_berth');


-- =============================================================================
-- 2. ADMINS
-- Platform administrators who manage operators, routes, and system monitoring.
-- =============================================================================

CREATE TABLE admins (
    id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    full_name     VARCHAR(100) NOT NULL,
    email         VARCHAR(255) NOT NULL UNIQUE,
    password_hash TEXT         NOT NULL,
    role          VARCHAR(30)  NOT NULL DEFAULT 'admin',  -- admin | super_admin
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);


-- =============================================================================
-- 3. USERS
-- All customer accounts. Central to bookings, seat locks, and notifications.
-- =============================================================================

CREATE TABLE users (
    id                 UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    full_name          VARCHAR(100) NOT NULL,
    email              VARCHAR(255) NOT NULL UNIQUE,
    email_verified     BOOLEAN      NOT NULL DEFAULT false,
    phone              VARCHAR(15)  UNIQUE,
    password_hash      TEXT         NOT NULL,
    age                SMALLINT     CHECK (age > 0 AND age < 121),
    gender             VARCHAR(10)  CHECK (gender IN ('Male', 'Female', 'Other')),
    status             VARCHAR(20)  NOT NULL DEFAULT 'active' CHECK (status IN ('active', 'suspended')),
    reset_token        TEXT,
    reset_token_expiry TIMESTAMPTZ,
    created_at         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at         TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_users_status ON users (status);


-- =============================================================================
-- 4. OPERATORS
-- Bus operator company accounts. Requires admin approval before platform access.
-- Status lifecycle: pending → approved | rejected; approved → disabled
-- =============================================================================

CREATE TABLE operators (
    id               UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    company_name     VARCHAR(150)    NOT NULL,
    email            VARCHAR(255)    NOT NULL UNIQUE,
    phone            VARCHAR(15)     NOT NULL,
    gst_number       VARCHAR(20)     UNIQUE,
    address          TEXT,
    password_hash    TEXT            NOT NULL,
    status           operator_status NOT NULL DEFAULT 'pending',
    approved_by      UUID            REFERENCES admins(id),
    approved_at      TIMESTAMPTZ,
    rejection_reason TEXT,
    created_at       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_operators_status ON operators (status);


-- =============================================================================
-- 5. ROUTES
-- Admin-defined source–destination pairs. Operators assign buses to routes.
-- =============================================================================

CREATE TABLE routes (
    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    source_city      VARCHAR(100) NOT NULL,
    destination_city VARCHAR(100) NOT NULL,
    distance_km      DECIMAL(8,2),
    estimated_hours  DECIMAL(4,1),
    is_active        BOOLEAN      NOT NULL DEFAULT true,
    created_by       UUID         NOT NULL REFERENCES admins(id),
    created_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE (source_city, destination_city)
);


-- =============================================================================
-- 6. BUSES
-- Individual bus listings per operator. Each bus is tied to a single route.
-- seat_layout JSONB example:
--   { "rows": 10, "cols": 4,
--     "layout": [
--       { "seat_number": "A1", "row": 1, "col": 1, "type": "seater" }, ...
--     ]
--   }
-- =============================================================================

CREATE TABLE buses (
    id                  UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    operator_id         UUID          NOT NULL REFERENCES operators(id),
    route_id            UUID          NOT NULL REFERENCES routes(id),
    registration_number VARCHAR(20)   NOT NULL UNIQUE,
    bus_type            VARCHAR(20)   NOT NULL,  -- AC | NonAC | Sleeper | SemiSleeper
    total_seats         SMALLINT      NOT NULL CHECK (total_seats > 0),
    departure_time      TIME          NOT NULL,
    arrival_time        TIME          NOT NULL,
    boarding_location   TEXT          NOT NULL,
    dropping_location   TEXT          NOT NULL,
    price_per_seat      DECIMAL(10,2) NOT NULL CHECK (price_per_seat >= 0),
    amenities           JSONB         NOT NULL DEFAULT '[]',  -- ['WiFi','USB','Water','Blanket']
    seat_layout         JSONB         NOT NULL,
    status              bus_status    NOT NULL DEFAULT 'active',
    created_at          TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_buses_route_status ON buses (route_id, status);


-- =============================================================================
-- 7. SEATS
-- Individual seat records, auto-generated when a bus is created.
-- ON DELETE CASCADE: seats are removed if the parent bus is deleted.
-- =============================================================================

CREATE TABLE seats (
    id           UUID           PRIMARY KEY DEFAULT gen_random_uuid(),
    bus_id       UUID           NOT NULL REFERENCES buses(id) ON DELETE CASCADE,
    seat_number  VARCHAR(10)    NOT NULL,
    seat_type    seat_type_enum NOT NULL DEFAULT 'seater',
    row_position SMALLINT       NOT NULL,
    col_position SMALLINT       NOT NULL,
    is_active    BOOLEAN        NOT NULL DEFAULT true,
    UNIQUE (bus_id, seat_number)
);

CREATE INDEX idx_seats_bus_active ON seats (bus_id, is_active);


-- =============================================================================
-- 8. BOOKINGS
-- Master booking record created after successful payment.
-- Status transitions: confirmed → cancelled | completed
-- =============================================================================

CREATE TABLE bookings (
    id                  UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             UUID          NOT NULL REFERENCES users(id),
    bus_id              UUID          NOT NULL REFERENCES buses(id),
    journey_date        DATE          NOT NULL,
    total_amount        DECIMAL(10,2) NOT NULL,
    status              booking_status NOT NULL DEFAULT 'confirmed',
    cancellation_reason TEXT,
    cancelled_at        TIMESTAMPTZ,
    ticket_pdf_path     TEXT,
    created_at          TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_bookings_user_status ON bookings (user_id, status);


-- =============================================================================
-- 9. BOOKING DETAILS
-- One row per seat per booking. Stores passenger details and price snapshot.
-- ON DELETE CASCADE: details removed when parent booking is deleted.
-- =============================================================================

CREATE TABLE booking_details (
    id               UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id       UUID          NOT NULL REFERENCES bookings(id) ON DELETE CASCADE,
    seat_id          UUID          NOT NULL REFERENCES seats(id),
    passenger_name   VARCHAR(100)  NOT NULL,
    passenger_age    SMALLINT      NOT NULL CHECK (passenger_age BETWEEN 1 AND 120),
    passenger_gender VARCHAR(10)   NOT NULL,  -- Male | Female | Other
    seat_price       DECIMAL(10,2) NOT NULL,
    CONSTRAINT unique_booking_seat UNIQUE (booking_id, seat_id)
);

CREATE INDEX idx_booking_details_booking ON booking_details (booking_id);


-- =============================================================================
-- 10. PAYMENTS
-- One-to-one with bookings. Stores payment gateway details.
-- Phase 1: dummy gateway (stripe fields NULL).
-- Phase 2: Stripe PaymentIntent ID stored for reconciliation.
-- =============================================================================

CREATE TABLE payments (
    id                  UUID           PRIMARY KEY DEFAULT gen_random_uuid(),
    booking_id          UUID           NOT NULL UNIQUE REFERENCES bookings(id),
    amount              DECIMAL(10,2)  NOT NULL,
    payment_method      payment_method_enum NOT NULL,  -- dummy | stripe_card | stripe_upi
    payment_status      payment_status NOT NULL DEFAULT 'pending',
    stripe_payment_id   VARCHAR(100),
    stripe_refund_id    VARCHAR(100),
    refund_amount       DECIMAL(10,2),
    refund_initiated_at TIMESTAMPTZ,
    paid_at             TIMESTAMPTZ,
    created_at          TIMESTAMPTZ    NOT NULL DEFAULT NOW()
);


-- =============================================================================
-- 11. SEAT LOCKS
-- Concurrency control table. Temporary reservations during checkout (10 min TTL).
-- Background job: DELETE FROM seat_locks WHERE lock_expiry < NOW() — runs every 60s.
-- After deletion, broadcast seat-released event via SignalR.
--
-- CRITICAL: Lock acquisition must use SELECT FOR UPDATE SKIP LOCKED inside a txn.
-- =============================================================================

CREATE TABLE seat_locks (
    id           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    seat_id      UUID        NOT NULL REFERENCES seats(id),
    user_id      UUID        NOT NULL REFERENCES users(id),
    bus_id       UUID        NOT NULL REFERENCES buses(id),
    journey_date DATE        NOT NULL,
    lock_expiry  TIMESTAMPTZ NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (seat_id, journey_date)
);

CREATE INDEX idx_seat_locks_expiry ON seat_locks (seat_id, journey_date, lock_expiry);


-- =============================================================================
-- 12. NOTIFICATIONS
-- Asynchronous email notification queue. Background job polls for pending rows
-- and sends via SendGrid.
-- =============================================================================

CREATE TABLE notifications (
    id             UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    recipient_id   UUID         NOT NULL,
    recipient_type VARCHAR(20)  NOT NULL,   -- user | operator
    type           VARCHAR(50)  NOT NULL,   -- booking_confirmed | cancelled | operator_disabled | password_reset
    subject        VARCHAR(255) NOT NULL,
    body           TEXT         NOT NULL,
    status         VARCHAR(20)  NOT NULL DEFAULT 'pending',  -- pending | sent | failed
    sent_at        TIMESTAMPTZ,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);


-- =============================================================================
-- END OF SCHEMA
-- =============================================================================
