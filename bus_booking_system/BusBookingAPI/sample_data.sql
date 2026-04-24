-- =============================================================================
-- BUS BOOKING SYSTEM — Sample Data v1.0
-- For testing schema.sql on PostgreSQL 16
-- Run AFTER schema.sql is applied to the database.
-- =============================================================================
-- UUID format: all hex (0–9, a–f) — no alphabetic prefixes
-- All password_hash values represent bcrypt of 'Test@1234' (cost 12) — dev only
-- =============================================================================


-- =============================================================================
-- 1. ADMINS
-- =============================================================================

INSERT INTO admins (id, full_name, email, password_hash, role, created_at) VALUES
(
    'a1000000-0000-0000-0000-000000000001',
    'Rajesh Kumar',
    'rajesh.admin@busbooking.in',
    '$2b$12$KIXGEKelLXXbIoGHk5j7p.examplehashfordevonly11111111111',
    'super_admin',
    NOW() - INTERVAL '180 days'
),
(
    'a1000000-0000-0000-0000-000000000002',
    'Priya Sharma',
    'priya.admin@busbooking.in',
    '$2b$12$KIXGEKelLXXbIoGHk5j7p.examplehashfordevonly22222222222',
    'admin',
    NOW() - INTERVAL '90 days'
);


-- =============================================================================
-- 2. USERS
-- =============================================================================

INSERT INTO users (id, full_name, email, email_verified, phone, password_hash, age, gender, status, created_at, updated_at) VALUES
(
    '20000000-0000-0000-0000-000000000001',
    'Amit Verma',
    'amit.verma@gmail.com',
    true,
    '9876543210',
    '$2b$12$KIXGEKelLXXbIoGHk5j7p.examplehashfordevonly33333333333',
    28,
    'Male',
    'active',
    NOW() - INTERVAL '60 days',
    NOW() - INTERVAL '60 days'
),
(
    '20000000-0000-0000-0000-000000000002',
    'Sneha Iyer',
    'sneha.iyer@gmail.com',
    true,
    '9123456780',
    '$2b$12$KIXGEKelLXXbIoGHk5j7p.examplehashfordevonly44444444444',
    24,
    'Female',
    'active',
    NOW() - INTERVAL '45 days',
    NOW() - INTERVAL '45 days'
),
(
    '20000000-0000-0000-0000-000000000003',
    'Kiran Patel',
    'kiran.patel@yahoo.com',
    false,
    NULL,
    '$2b$12$KIXGEKelLXXbIoGHk5j7p.examplehashfordevonly55555555555',
    35,
    'Male',
    'active',
    NOW() - INTERVAL '10 days',
    NOW() - INTERVAL '10 days'
),
(
    '20000000-0000-0000-0000-000000000004',
    'Meera Nair',
    'meera.nair@outlook.com',
    true,
    '9988776655',
    '$2b$12$KIXGEKelLXXbIoGHk5j7p.examplehashfordevonly66666666666',
    31,
    'Female',
    'suspended',
    NOW() - INTERVAL '120 days',
    NOW() - INTERVAL '5 days'
);


-- =============================================================================
-- 3. OPERATORS
-- =============================================================================

INSERT INTO operators (id, company_name, email, phone, gst_number, address, password_hash, status, approved_by, approved_at, rejection_reason, created_at, updated_at) VALUES
(
    '30000000-0000-0000-0000-000000000001',
    'SunTravels Pvt Ltd',
    'info@suntravels.in',
    '04422334455',
    '33AABCS1234F1ZK',
    '12, Anna Salai, Chennai, Tamil Nadu - 600002',
    '$2b$12$KIXGEKelLXXbIoGHk5j7p.examplehashfordevonly77777777777',
    'approved',
    'a1000000-0000-0000-0000-000000000001',
    NOW() - INTERVAL '50 days',
    NULL,
    NOW() - INTERVAL '55 days',
    NOW() - INTERVAL '50 days'
),
(
    '30000000-0000-0000-0000-000000000002',
    'KarnatakaExpress Tours',
    'contact@karnatakaexpress.in',
    '08012233445',
    '29AABCK5678G1ZM',
    '45, MG Road, Bangalore, Karnataka - 560001',
    '$2b$12$KIXGEKelLXXbIoGHk5j7p.examplehashfordevonly88888888888',
    'approved',
    'a1000000-0000-0000-0000-000000000002',
    NOW() - INTERVAL '30 days',
    NULL,
    NOW() - INTERVAL '35 days',
    NOW() - INTERVAL '30 days'
),
(
    '30000000-0000-0000-0000-000000000003',
    'SwiftRide Travels',
    'ops@swiftride.in',
    '02211223344',
    NULL,
    '78, Dadar West, Mumbai, Maharashtra - 400028',
    '$2b$12$KIXGEKelLXXbIoGHk5j7p.examplehashfordevonly99999999999',
    'pending',
    NULL,
    NULL,
    NULL,
    NOW() - INTERVAL '2 days',
    NOW() - INTERVAL '2 days'
),
(
    '30000000-0000-0000-0000-000000000004',
    'SpeedLink Buses',
    'admin@speedlink.in',
    '04023344556',
    '36AABCS9999H1ZP',
    '22, Begumpet, Hyderabad, Telangana - 500016',
    '$2b$12$KIXGEKelLXXbIoGHk5j7p.examplehashfordevonly00000000000',
    'rejected',
    'a1000000-0000-0000-0000-000000000001',
    NOW() - INTERVAL '20 days',
    'GST number could not be verified. Please resubmit with valid documents.',
    NOW() - INTERVAL '25 days',
    NOW() - INTERVAL '20 days'
);


-- =============================================================================
-- 4. ROUTES
-- =============================================================================

INSERT INTO routes (id, source_city, destination_city, distance_km, estimated_hours, is_active, created_by, created_at, updated_at) VALUES
(
    '40000000-0000-0000-0000-000000000001',
    'Chennai',
    'Bangalore',
    350.00,
    6.5,
    true,
    'a1000000-0000-0000-0000-000000000001',
    NOW() - INTERVAL '100 days',
    NOW() - INTERVAL '100 days'
),
(
    '40000000-0000-0000-0000-000000000002',
    'Bangalore',
    'Hyderabad',
    570.00,
    9.0,
    true,
    'a1000000-0000-0000-0000-000000000001',
    NOW() - INTERVAL '100 days',
    NOW() - INTERVAL '100 days'
),
(
    '40000000-0000-0000-0000-000000000003',
    'Mumbai',
    'Pune',
    150.00,
    3.0,
    true,
    'a1000000-0000-0000-0000-000000000002',
    NOW() - INTERVAL '80 days',
    NOW() - INTERVAL '80 days'
),
(
    '40000000-0000-0000-0000-000000000004',
    'Chennai',
    'Madurai',
    460.00,
    8.0,
    false,
    'a1000000-0000-0000-0000-000000000002',
    NOW() - INTERVAL '60 days',
    NOW() - INTERVAL '5 days'
);


-- =============================================================================
-- 5. BUSES  (Fix #5: single-line JSON for seat_layout)
-- =============================================================================

INSERT INTO buses (id, operator_id, route_id, registration_number, bus_type, total_seats, departure_time, arrival_time, boarding_location, dropping_location, price_per_seat, amenities, seat_layout, status, created_at, updated_at) VALUES
(
    '50000000-0000-0000-0000-000000000001',
    '30000000-0000-0000-0000-000000000001',
    '40000000-0000-0000-0000-000000000001',
    'TN01AB1234',
    'AC',
    40,
    '21:00',
    '03:30',
    'Koyambedu Bus Stand, Chennai',
    'Majestic Bus Stand, Bangalore',
    750.00,
    '["WiFi","USB","Water","Blanket"]',
    '{"rows":10,"cols":4,"layout":[{"seat_number":"A1","row":1,"col":1,"type":"seater"},{"seat_number":"A2","row":1,"col":2,"type":"seater"},{"seat_number":"A3","row":1,"col":3,"type":"seater"},{"seat_number":"A4","row":1,"col":4,"type":"seater"},{"seat_number":"B1","row":2,"col":1,"type":"seater"},{"seat_number":"B2","row":2,"col":2,"type":"seater"},{"seat_number":"B3","row":2,"col":3,"type":"seater"},{"seat_number":"B4","row":2,"col":4,"type":"seater"}]}',
    'active',
    NOW() - INTERVAL '48 days',
    NOW() - INTERVAL '48 days'
),
(
    '50000000-0000-0000-0000-000000000002',
    '30000000-0000-0000-0000-000000000001',
    '40000000-0000-0000-0000-000000000001',
    'TN01AB5678',
    'Sleeper',
    30,
    '22:30',
    '05:00',
    'Koyambedu Bus Stand, Chennai',
    'Silk Board, Bangalore',
    1100.00,
    '["USB","Water","Blanket"]',
    '{"rows":10,"cols":3,"layout":[{"seat_number":"L1","row":1,"col":1,"type":"lower_berth"},{"seat_number":"U1","row":1,"col":2,"type":"upper_berth"},{"seat_number":"L2","row":2,"col":1,"type":"lower_berth"},{"seat_number":"U2","row":2,"col":2,"type":"upper_berth"}]}',
    'active',
    NOW() - INTERVAL '40 days',
    NOW() - INTERVAL '40 days'
),
(
    '50000000-0000-0000-0000-000000000003',
    '30000000-0000-0000-0000-000000000002',
    '40000000-0000-0000-0000-000000000002',
    'KA01CD9012',
    'SemiSleeper',
    45,
    '20:00',
    '05:00',
    'Majestic Bus Stand, Bangalore',
    'MGBS Hyderabad',
    900.00,
    '["WiFi","USB"]',
    '{"rows":9,"cols":5,"layout":[{"seat_number":"A1","row":1,"col":1,"type":"seater"},{"seat_number":"A2","row":1,"col":2,"type":"seater"},{"seat_number":"A3","row":1,"col":3,"type":"seater"},{"seat_number":"A4","row":1,"col":4,"type":"seater"},{"seat_number":"A5","row":1,"col":5,"type":"seater"}]}',
    'active',
    NOW() - INTERVAL '25 days',
    NOW() - INTERVAL '25 days'
),
(
    '50000000-0000-0000-0000-000000000004',
    '30000000-0000-0000-0000-000000000002',
    '40000000-0000-0000-0000-000000000002',
    'KA01CD3456',
    'NonAC',
    50,
    '06:00',
    '15:30',
    'Majestic Bus Stand, Bangalore',
    'MGBS Hyderabad',
    500.00,
    '[]',
    '{"rows":10,"cols":5,"layout":[{"seat_number":"A1","row":1,"col":1,"type":"seater"},{"seat_number":"A2","row":1,"col":2,"type":"seater"},{"seat_number":"A3","row":1,"col":3,"type":"seater"},{"seat_number":"A4","row":1,"col":4,"type":"seater"},{"seat_number":"A5","row":1,"col":5,"type":"seater"}]}',
    'maintenance',
    NOW() - INTERVAL '20 days',
    NOW() - INTERVAL '3 days'
);


-- =============================================================================
-- 6. SEATS
-- Bus 1 (50000000-…-0001): A1–A4, B1–B4
-- Bus 2 (50000000-…-0002): L1, U1, L2, U2
-- Bus 3 (50000000-…-0003): A1–A5
-- =============================================================================

-- Bus 1 seats
INSERT INTO seats (id, bus_id, seat_number, seat_type, row_position, col_position, is_active) VALUES
('60000000-0001-0000-0000-000000000001', '50000000-0000-0000-0000-000000000001', 'A1', 'seater', 1, 1, true),
('60000000-0001-0000-0000-000000000002', '50000000-0000-0000-0000-000000000001', 'A2', 'seater', 1, 2, true),
('60000000-0001-0000-0000-000000000003', '50000000-0000-0000-0000-000000000001', 'A3', 'seater', 1, 3, true),
('60000000-0001-0000-0000-000000000004', '50000000-0000-0000-0000-000000000001', 'A4', 'seater', 1, 4, true),
('60000000-0001-0000-0000-000000000005', '50000000-0000-0000-0000-000000000001', 'B1', 'seater', 2, 1, true),
('60000000-0001-0000-0000-000000000006', '50000000-0000-0000-0000-000000000001', 'B2', 'seater', 2, 2, false),  -- disabled seat
('60000000-0001-0000-0000-000000000007', '50000000-0000-0000-0000-000000000001', 'B3', 'seater', 2, 3, true),
('60000000-0001-0000-0000-000000000008', '50000000-0000-0000-0000-000000000001', 'B4', 'seater', 2, 4, true);

-- Bus 2 seats (sleeper)
INSERT INTO seats (id, bus_id, seat_number, seat_type, row_position, col_position, is_active) VALUES
('60000000-0002-0000-0000-000000000001', '50000000-0000-0000-0000-000000000002', 'L1', 'lower_berth', 1, 1, true),
('60000000-0002-0000-0000-000000000002', '50000000-0000-0000-0000-000000000002', 'U1', 'upper_berth', 1, 2, true),
('60000000-0002-0000-0000-000000000003', '50000000-0000-0000-0000-000000000002', 'L2', 'lower_berth', 2, 1, true),
('60000000-0002-0000-0000-000000000004', '50000000-0000-0000-0000-000000000002', 'U2', 'upper_berth', 2, 2, true);

-- Bus 3 seats
INSERT INTO seats (id, bus_id, seat_number, seat_type, row_position, col_position, is_active) VALUES
('60000000-0003-0000-0000-000000000001', '50000000-0000-0000-0000-000000000003', 'A1', 'seater', 1, 1, true),
('60000000-0003-0000-0000-000000000002', '50000000-0000-0000-0000-000000000003', 'A2', 'seater', 1, 2, true),
('60000000-0003-0000-0000-000000000003', '50000000-0000-0000-0000-000000000003', 'A3', 'seater', 1, 3, true),
('60000000-0003-0000-0000-000000000004', '50000000-0000-0000-0000-000000000003', 'A4', 'seater', 1, 4, true),
('60000000-0003-0000-0000-000000000005', '50000000-0000-0000-0000-000000000003', 'A5', 'seater', 1, 5, true);


-- =============================================================================
-- 7. BOOKINGS
-- =============================================================================

INSERT INTO bookings (id, user_id, bus_id, journey_date, total_amount, status, cancellation_reason, cancelled_at, ticket_pdf_path, created_at, updated_at) VALUES
(
    'b0000000-0000-0000-0000-000000000001',
    '20000000-0000-0000-0000-000000000001',
    '50000000-0000-0000-0000-000000000001',
    CURRENT_DATE + INTERVAL '5 days',
    1500.00,
    'confirmed',
    NULL,
    NULL,
    '/tickets/b0000000-0000-0000-0000-000000000001.pdf',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '1 day'
),
(
    'b0000000-0000-0000-0000-000000000002',
    '20000000-0000-0000-0000-000000000002',
    '50000000-0000-0000-0000-000000000002',
    CURRENT_DATE + INTERVAL '10 days',
    1100.00,
    'confirmed',
    NULL,
    NULL,
    '/tickets/b0000000-0000-0000-0000-000000000002.pdf',
    NOW() - INTERVAL '2 hours',
    NOW() - INTERVAL '2 hours'
),
(
    'b0000000-0000-0000-0000-000000000003',
    '20000000-0000-0000-0000-000000000001',
    '50000000-0000-0000-0000-000000000003',
    CURRENT_DATE - INTERVAL '10 days',
    1800.00,
    'completed',
    NULL,
    NULL,
    '/tickets/b0000000-0000-0000-0000-000000000003.pdf',
    NOW() - INTERVAL '15 days',
    NOW() - INTERVAL '9 days'
),
(
    'b0000000-0000-0000-0000-000000000004',
    '20000000-0000-0000-0000-000000000003',
    '50000000-0000-0000-0000-000000000001',
    CURRENT_DATE + INTERVAL '3 days',
    750.00,
    'cancelled',
    'Change of travel plans',
    NOW() - INTERVAL '3 hours',
    NULL,
    NOW() - INTERVAL '6 hours',
    NOW() - INTERVAL '3 hours'
);


-- =============================================================================
-- 8. BOOKING DETAILS
-- Fix #2: UNIQUE (booking_id, seat_id) — each seat appears only once per booking
-- =============================================================================

-- Booking 1: Amit books A1 and A2 (himself + companion)
INSERT INTO booking_details (id, booking_id, seat_id, passenger_name, passenger_age, passenger_gender, seat_price) VALUES
(
    'd0000000-0000-0000-0000-000000000001',
    'b0000000-0000-0000-0000-000000000001',
    '60000000-0001-0000-0000-000000000001',
    'Amit Verma',
    28,
    'Male',
    750.00
),
(
    'd0000000-0000-0000-0000-000000000002',
    'b0000000-0000-0000-0000-000000000001',
    '60000000-0001-0000-0000-000000000002',
    'Riya Verma',
    25,
    'Female',
    750.00
);

-- Booking 2: Sneha books lower berth L1
INSERT INTO booking_details (id, booking_id, seat_id, passenger_name, passenger_age, passenger_gender, seat_price) VALUES
(
    'd0000000-0000-0000-0000-000000000003',
    'b0000000-0000-0000-0000-000000000002',
    '60000000-0002-0000-0000-000000000001',
    'Sneha Iyer',
    24,
    'Female',
    1100.00
);

-- Booking 3: Amit's completed journey — 2 seats on Bus 3
INSERT INTO booking_details (id, booking_id, seat_id, passenger_name, passenger_age, passenger_gender, seat_price) VALUES
(
    'd0000000-0000-0000-0000-000000000004',
    'b0000000-0000-0000-0000-000000000003',
    '60000000-0003-0000-0000-000000000001',
    'Amit Verma',
    28,
    'Male',
    900.00
),
(
    'd0000000-0000-0000-0000-000000000005',
    'b0000000-0000-0000-0000-000000000003',
    '60000000-0003-0000-0000-000000000002',
    'Suresh Kumar',
    55,
    'Male',
    900.00
);

-- Booking 4: Kiran's cancelled booking — seat A3
INSERT INTO booking_details (id, booking_id, seat_id, passenger_name, passenger_age, passenger_gender, seat_price) VALUES
(
    'd0000000-0000-0000-0000-000000000006',
    'b0000000-0000-0000-0000-000000000004',
    '60000000-0001-0000-0000-000000000003',
    'Kiran Patel',
    35,
    'Male',
    750.00
);


-- =============================================================================
-- 9. PAYMENTS  (Fix #4: payment_method uses payment_method_enum values)
-- =============================================================================

INSERT INTO payments (id, booking_id, amount, payment_method, payment_status, stripe_payment_id, stripe_refund_id, refund_amount, refund_initiated_at, paid_at, created_at) VALUES
(
    'e0000000-0000-0000-0000-000000000001',
    'b0000000-0000-0000-0000-000000000001',
    1500.00,
    'dummy',
    'success',
    NULL, NULL, NULL, NULL,
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '1 day'
),
(
    'e0000000-0000-0000-0000-000000000002',
    'b0000000-0000-0000-0000-000000000002',
    1100.00,
    'stripe_card',
    'success',
    'pi_3OqABCDEFGHIJKLM12345678',
    NULL, NULL, NULL,
    NOW() - INTERVAL '2 hours',
    NOW() - INTERVAL '2 hours'
),
(
    'e0000000-0000-0000-0000-000000000003',
    'b0000000-0000-0000-0000-000000000003',
    1800.00,
    'stripe_upi',
    'success',
    'pi_3OqABCDEFGHIJKLM87654321',
    NULL, NULL, NULL,
    NOW() - INTERVAL '15 days',
    NOW() - INTERVAL '15 days'
),
(
    'e0000000-0000-0000-0000-000000000004',
    'b0000000-0000-0000-0000-000000000004',
    750.00,
    'dummy',
    'refunded',
    NULL, NULL,
    750.00,
    NOW() - INTERVAL '3 hours',
    NOW() - INTERVAL '6 hours',
    NOW() - INTERVAL '6 hours'
);


-- =============================================================================
-- 10. SEAT LOCKS  (two users currently in checkout flow)
-- =============================================================================

INSERT INTO seat_locks (id, seat_id, user_id, bus_id, journey_date, lock_expiry, created_at) VALUES
(
    'f0000000-0000-0000-0000-000000000001',
    '60000000-0001-0000-0000-000000000004',   -- seat A4 on Bus 1
    '20000000-0000-0000-0000-000000000002',   -- Sneha in checkout
    '50000000-0000-0000-0000-000000000001',
    CURRENT_DATE + INTERVAL '5 days',
    NOW() + INTERVAL '8 minutes',
    NOW() - INTERVAL '2 minutes'
),
(
    'f0000000-0000-0000-0000-000000000002',
    '60000000-0001-0000-0000-000000000005',   -- seat B1 on Bus 1
    '20000000-0000-0000-0000-000000000003',   -- Kiran in checkout
    '50000000-0000-0000-0000-000000000001',
    CURRENT_DATE + INTERVAL '5 days',
    NOW() + INTERVAL '9 minutes',
    NOW() - INTERVAL '1 minute'
);


-- =============================================================================
-- 11. NOTIFICATIONS
-- =============================================================================

INSERT INTO notifications (id, recipient_id, recipient_type, type, subject, body, status, sent_at, created_at) VALUES
(
    'c0000000-0000-0000-0000-000000000001',
    '20000000-0000-0000-0000-000000000001',
    'user',
    'booking_confirmed',
    'Your booking is confirmed!',
    '<h1>Booking Confirmed</h1><p>Hi Amit, your journey from Chennai to Bangalore on Bus TN01AB1234 is confirmed. Booking ID: b0000000-0000-0000-0000-000000000001.</p>',
    'sent',
    NOW() - INTERVAL '1 day',
    NOW() - INTERVAL '1 day'
),
(
    'c0000000-0000-0000-0000-000000000002',
    '20000000-0000-0000-0000-000000000002',
    'user',
    'booking_confirmed',
    'Your booking is confirmed!',
    '<h1>Booking Confirmed</h1><p>Hi Sneha, your sleeper seat on TN01AB5678 from Chennai to Bangalore is confirmed. Booking ID: b0000000-0000-0000-0000-000000000002.</p>',
    'sent',
    NOW() - INTERVAL '2 hours',
    NOW() - INTERVAL '2 hours'
),
(
    'c0000000-0000-0000-0000-000000000003',
    '20000000-0000-0000-0000-000000000003',
    'user',
    'cancelled',
    'Your booking has been cancelled',
    '<h1>Booking Cancelled</h1><p>Hi Kiran, your booking has been cancelled. Refund of Rs.750 will be processed within 5-7 business days.</p>',
    'sent',
    NOW() - INTERVAL '3 hours',
    NOW() - INTERVAL '3 hours'
),
(
    'c0000000-0000-0000-0000-000000000004',
    '30000000-0000-0000-0000-000000000004',
    'operator',
    'operator_disabled',
    'Your operator application has been rejected',
    '<h1>Application Rejected</h1><p>Dear SpeedLink Buses, your application has been rejected. Reason: GST number could not be verified. Please resubmit with valid documents.</p>',
    'sent',
    NOW() - INTERVAL '20 days',
    NOW() - INTERVAL '20 days'
),
(
    'c0000000-0000-0000-0000-000000000005',
    '20000000-0000-0000-0000-000000000004',
    'user',
    'password_reset',
    'Reset your BusBooking password',
    '<h1>Password Reset</h1><p>Hi Meera, click the link below to reset your password. This link expires in 30 minutes.</p>',
    'failed',
    NULL,
    NOW() - INTERVAL '5 days'
);


-- =============================================================================
-- VERIFICATION QUERIES (uncomment to run)
-- =============================================================================

-- Row counts per table
-- SELECT 'admins'           AS tbl, COUNT(*) FROM admins
-- UNION ALL SELECT 'users',           COUNT(*) FROM users
-- UNION ALL SELECT 'operators',       COUNT(*) FROM operators
-- UNION ALL SELECT 'routes',          COUNT(*) FROM routes
-- UNION ALL SELECT 'buses',           COUNT(*) FROM buses
-- UNION ALL SELECT 'seats',           COUNT(*) FROM seats
-- UNION ALL SELECT 'bookings',        COUNT(*) FROM bookings
-- UNION ALL SELECT 'booking_details', COUNT(*) FROM booking_details
-- UNION ALL SELECT 'payments',        COUNT(*) FROM payments
-- UNION ALL SELECT 'seat_locks',      COUNT(*) FROM seat_locks
-- UNION ALL SELECT 'notifications',   COUNT(*) FROM notifications;

-- Full booking summary with payment status
-- SELECT b.id, u.full_name, bs.registration_number, b.journey_date,
--        b.status AS booking_status, b.total_amount, p.payment_status, p.payment_method
-- FROM bookings b
-- JOIN users    u  ON u.id  = b.user_id
-- JOIN buses    bs ON bs.id = b.bus_id
-- JOIN payments p  ON p.booking_id = b.id
-- ORDER BY b.created_at DESC;

-- Active seat locks with countdown
-- SELECT sl.id, s.seat_number, u.full_name, sl.journey_date,
--        (sl.lock_expiry - NOW()) AS time_remaining
-- FROM seat_locks sl
-- JOIN seats s ON s.id = sl.seat_id
-- JOIN users u ON u.id = sl.user_id
-- WHERE sl.lock_expiry > NOW();

-- =============================================================================
-- END OF SAMPLE DATA
-- =============================================================================
