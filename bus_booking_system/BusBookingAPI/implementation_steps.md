Here's the full content as markdown:

```markdown
# BUS BOOKING SYSTEM
## Backend Implementation & API Design
**Phase-wise Development Guide | .NET 8 Web API + PostgreSQL 16**

| Setting | Value |
|---|---|
| Stack | .NET 8 · PostgreSQL 16 · Angular 17 |
| Version | 1.0 \| April 2026 |
| Phases | Phase 1 · Phase 2 · Phase 3 |

---

## Table of Contents
1. [Project Architecture Overview](#1-project-architecture-overview)
2. [Phase Overview & Delivery Plan](#2-phase-overview--delivery-plan)
3. [Authentication Module — All Roles](#3-authentication-module--all-roles)
4. [Phase 1 — Bus Search & Browse](#4-phase-1--bus-search--browse)
5. [Phase 1 — Seat Locking (Critical)](#5-phase-1--seat-locking-critical)
6. [Phase 1 — Booking Flow](#6-phase-1--booking-flow)
7. [Phase 1 — Operator Module](#7-phase-1--operator-module)
8. [Phase 1 — Admin Module](#8-phase-1--admin-module)
9. [Phase 2 — Stripe Payment Integration](#9-phase-2--stripe-payment-integration)
10. [Phase 2 — Email Notifications](#10-phase-2--email-notifications)
11. [PDF Ticket Generation (QuestPDF)](#11-pdf-ticket-generation-questpdf)
12. [API Endpoint Master Reference](#12-api-endpoint-master-reference)
13. [Error Response Standard](#13-error-response-standard)
14. [Non-Functional Implementation Notes](#14-non-functional-implementation-notes)
15. [Development Checklist by Phase](#15-development-checklist-by-phase)

---

# 1. Project Architecture Overview

The Bus Booking System follows a layered .NET 8 Web API architecture with clean separation of concerns. All business logic lives in the Service layer, data access is handled exclusively through Entity Framework Core 8 repositories, and controllers are thin — routing and auth only.

## 1.1 Solution Structure

| Project / Layer | Responsibility | Key Contents |
|---|---|---|
| BusBooking.API | HTTP entry point — routing, middleware, DI config | Controllers, Hubs, Program.cs, appsettings.json |
| BusBooking.Application | Business logic, use-cases, orchestration | Services, DTOs, Validators (FluentValidation) |
| BusBooking.Domain | Pure domain models and enums — no EF references | Entities, Enums, Domain Interfaces |
| BusBooking.Infrastructure | Data access, external services | EF DbContext, Repositories, Email, PDF, Hangfire jobs |
| BusBooking.Shared | Cross-cutting utilities | JWT helper, Result\<T\>, PaginatedList\<T\> |

## 1.2 Middleware Pipeline (Program.cs order)

```csharp
app.UseHttpsRedirection()
app.UseCors("AllowAngularClient")
app.UseAuthentication()    // JWT bearer token
app.UseAuthorization()     // RBAC policy enforcement
app.UseRateLimiter()       // auth endpoint brute-force protection
app.MapControllers()
app.MapHub<SeatAvailabilityHub>("/hubs/seats")
app.UseHangfireDashboard("/hangfire")  // admin-only policy
```

## 1.3 JWT Configuration

Every API controller is decorated with `[Authorize]`. Role-specific endpoints layer `[Authorize(Roles = "User"|"Operator"|"Admin")]` on top. The token payload carries `sub` (user UUID), `role`, and `email` claims.

| Setting | Value |
|---|---|
| Issuer | BusBookingSystem |
| Audience | BusBookingClients |
| Expiry | 24 hours (configurable via appsettings) |
| Algorithm | HMAC SHA-256 |
| Secret storage | Environment variable / Azure Key Vault (never appsettings) |

> ⚠ **SECURITY:** Never commit JWT secrets to source control. Use `dotnet user-secrets` locally and Azure Key Vault in production.

---

# 2. Phase Overview & Delivery Plan

### PHASE 1 — MVP Core (Weeks 1–3)
- User auth (register / login / forgot-password)
- Bus search & browse (no auth required)
- Seat layout display + real-time availability (SignalR)
- Seat locking — SELECT FOR UPDATE SKIP LOCKED
- Booking flow with passenger details
- Dummy payment gateway (always succeeds)
- PDF ticket generation (QuestPDF)
- Booking history & cancellation
- Operator: add / edit / status-control buses
- Admin: approve/reject operators, manage routes

### PHASE 2 — Payments & Notifications (Weeks 4–5)
- Stripe Card & UPI payment integration
- Stripe Refund API on cancellation
- Email notifications via SendGrid (booking confirm, cancel, operator changes)
- QR code embedded in PDF ticket
- Admin advanced revenue dashboard (charts, operator breakdown)
- Operator: CSV export of passenger manifest

### PHASE 3 — Advanced Features (Weeks 6+)
- Google Maps live bus tracking
- User wallet / credits system
- Coupon & discount code engine
- Ratings & reviews for operators and buses
- Angular PWA / mobile app
- AI-based route recommendations
- Read-replica PostgreSQL for search scaling

---

# 3. Authentication Module — All Roles

## 3.1 User Registration

**Endpoint:** `POST /api/v1/auth/register`

| Field | Validation Rule |
|---|---|
| full_name | Required, 2–100 chars |
| email | Required, unique, valid RFC email format |
| phone | Optional, 10-digit Indian mobile (`^[6-9]\d{9}$`) |
| password | Min 8 chars, ≥1 uppercase, ≥1 digit, ≥1 special char |
| confirm_password | Must match password field |

### Request / Response Contract

```
POST /api/v1/auth/register
Body: { full_name, email, phone?, password, confirm_password }

201 Created  → { message: "Registration successful" }
409 Conflict → { error: "Email already registered" }
422 Unprocessable → { errors: [ { field, message } ] }
```

### Service Implementation Notes
- Hash password with `BCrypt.Net` (cost factor 12) before persisting
- Generate `email_verified = false` on creation (OTP verification is Phase 2)
- Check email uniqueness in a single DB round-trip before insertion

---

## 3.2 Login (User / Operator / Admin)

Same endpoint handles all three roles. JWT role claim determines access tier.

| Role | Endpoint | Extra Check |
|---|---|---|
| User | POST /api/v1/auth/login | status = 'active' |
| Operator | POST /api/v1/auth/operator/login | status = 'approved' |
| Admin | POST /api/v1/auth/admin/login | role IN ('admin','super_admin') |

```
POST /api/v1/auth/login
Body: { email, password }

200 OK  → { token: "eyJ...", expires_at: "2026-04-25T10:00:00Z", role: "User" }
401 Unauthorized → { error: "Invalid credentials" }
403 Forbidden    → { error: "Account suspended / pending approval" }
429 Too Many Requests → { error: "Too many attempts. Retry after 15 min" }
```

> ⚡ **Rate Limiting:** 5 failed attempts within 5 minutes triggers a 15-minute lockout. Implement via `IMemoryCache` keyed on IP+email, or use ASP.NET Core rate limiter middleware with a fixed window policy.

---

## 3.3 Forgot Password / Reset Password

```
POST /api/v1/auth/forgot-password
Body: { email }
200 OK → { message: "Reset link sent if email exists" }  // never reveal existence

POST /api/v1/auth/reset-password
Body: { token, new_password, confirm_password }
200 OK  → { message: "Password updated successfully" }
400 Bad Request → { error: "Token expired or already used" }
```

- Token: `gen_random_uuid()` stored in `users.reset_token`
- Expiry: `reset_token_expiry = NOW() + 30 minutes`
- After use: null out both `reset_token` and `reset_token_expiry` (single-use)

---

# 4. Phase 1 — Bus Search & Browse

## 4.1 Search Buses

This is the most-hit public endpoint. No authentication required. Combines routes, buses, and seat-lock aggregation in a single optimised query.

| Parameter | Required | Type | Notes |
|---|---|---|---|
| source | Yes | string | Matched against routes.source_city (ILIKE) |
| destination | Yes | string | Matched against routes.destination_city (ILIKE) |
| journey_date | Yes | date | Must be today or future. Format: YYYY-MM-DD |
| bus_type | No | string | AC \| NonAC \| Sleeper \| SemiSleeper |
| min_price | No | decimal | Filter on buses.price_per_seat ≥ min |
| max_price | No | decimal | Filter on buses.price_per_seat ≤ max |
| page | No | int | Default: 1 |
| page_size | No | int | Default: 10, Max: 50 |

```
GET /api/v1/buses/search?source=Chennai&destination=Bangalore&journey_date=2026-05-01

200 OK Response:
{
  total: 12,  page: 1,  page_size: 10,
  results: [
    {
      bus_id, operator_name, bus_type, registration_number,
      departure_time, arrival_time, duration_hours,
      total_seats, available_seats,  // derived: total - locked - booked
      price_per_seat, amenities,
      boarding_location, dropping_location
    }
  ]
}
```

### SQL Logic (EF Core equivalent)

```sql
SELECT b.*, COUNT(s.id) AS total_seats,
  COUNT(s.id) - COUNT(sl.id) - COUNT(bd.id) AS available_seats
FROM buses b
JOIN routes r ON r.id = b.route_id
JOIN seats s ON s.bus_id = b.id AND s.is_active = true
LEFT JOIN seat_locks sl ON sl.seat_id = s.id
  AND sl.journey_date = @journey_date AND sl.lock_expiry > NOW()
LEFT JOIN booking_details bd ON bd.seat_id = s.id
  JOIN bookings bk ON bk.id = bd.booking_id
  AND bk.journey_date = @journey_date AND bk.status = 'confirmed'
WHERE r.source_city ILIKE @source AND r.destination_city ILIKE @destination
  AND b.status = 'active' AND r.is_active = true
GROUP BY b.id
```

---

## 4.2 Get Bus Details + Seat Layout

```
GET /api/v1/buses/{id}?journey_date=2026-05-01

200 OK Response:
{
  bus_id, operator_name, bus_type, amenities,
  departure_time, arrival_time, boarding_location, dropping_location,
  seat_layout: {
    rows: 10, cols: 4,
    seats: [
      { seat_id, seat_number, seat_type, row, col,
        status: "available" | "locked" | "booked" }
    ]
  }
}
```

> **Performance:** Cache the static bus object (TTL 5 min). Seat status is always fetched live — never cached — to reflect real-time locking.

---

# 5. Phase 1 — Seat Locking (Critical)

> 🔴 **CRITICAL:** Seat locking is the most technically sensitive feature. A race condition here causes double bookings. Implementation MUST use PostgreSQL row-level locking inside an explicit transaction. Never use application-level locking.

## 5.1 Lock Seats

```
POST /api/v1/seats/lock
Auth: Bearer <UserToken>
Body: { bus_id, journey_date, seat_ids: ["uuid1", "uuid2"] }

201 Created → { lock_expiry: "2026-05-01T10:10:00Z", locked_seats: ["uuid1", "uuid2"] }
409 Conflict → { error: "Seat A3 is already locked or booked" }
```

### Transaction Implementation (.NET / EF Core)

```sql
BEGIN TRANSACTION (Serializable isolation)

For each seat_id in seat_ids:
  SELECT id FROM seat_locks
  WHERE seat_id = @seat_id AND journey_date = @journey_date
  AND lock_expiry > NOW()
  FOR UPDATE SKIP LOCKED

  If row found → ROLLBACK → return 409 Conflict

  INSERT INTO seat_locks (seat_id, user_id, bus_id, journey_date, lock_expiry)
  VALUES (@seat_id, @user_id, @bus_id, @journey_date, NOW() + INTERVAL '10 minutes')

COMMIT TRANSACTION

After commit: broadcast SeatLocked event via SignalR hub to all
             clients subscribed to bus_{bus_id}_{journey_date}
```

---

## 5.2 Release Lock (Manual)

```
DELETE /api/v1/seats/lock
Auth: Bearer <UserToken>
Body: { bus_id, journey_date, seat_ids: [...] }

200 OK → { message: "Seats released" }
403 Forbidden → if user_id on lock ≠ requesting user
```

---

## 5.3 Background Lock Cleanup (Hangfire Job)

```sql
-- Runs every 60 seconds via Hangfire RecurringJob

DELETE FROM seat_locks WHERE lock_expiry < NOW()
RETURNING seat_id, bus_id, journey_date   -- collect for SignalR

For each deleted lock:
  Broadcast SeatReleased(seat_id) to group bus_{bus_id}_{journey_date}
```

> **SignalR Group Naming Convention:** `bus_{bus_id}_{journey_date}`. Clients join this group on the seat selection page and leave on navigation away. Angular client updates NgRx seat state reactively on each incoming event.

---

# 6. Phase 1 — Booking Flow

## 6.1 Create Booking

Called after successful payment confirmation (or dummy payment in Phase 1). This operation is atomic — booking record, booking details, payment record, seat lock removal, and PDF generation all succeed or all rollback.

```
POST /api/v1/bookings
Auth: Bearer <UserToken>
Body:
{
  bus_id, journey_date,
  passengers: [
    { seat_id, passenger_name, passenger_age, passenger_gender }
  ],
  payment: { method: "dummy", amount: 1200.00 }
}

201 Created →
{
  booking_id, booking_status: "confirmed",
  total_amount, ticket_download_url: "/api/v1/bookings/{id}/ticket"
}

400 Bad Request → { error: "Seat lock expired for A3. Please re-select." }
409 Conflict   → { error: "Seat already booked" }
```

### Atomic Transaction Steps
1. Validate all seat locks still exist and belong to this user
2. INSERT into `bookings` → get `booking_id`
3. INSERT one row per seat into `booking_details`
4. INSERT into `payments` (status = `success` for Phase 1)
5. DELETE `seat_locks` for these seats
6. COMMIT transaction
7. *(post-commit, async)* Generate PDF ticket via QuestPDF, upload to storage, update `bookings.ticket_pdf_path`
8. *(post-commit, async)* Queue `booking_confirmed` notification in `notifications` table

---

## 6.2 Booking History

```
GET /api/v1/bookings?status=upcoming|completed|cancelled&page=1&page_size=10
Auth: Bearer <UserToken>

200 OK →
{
  total, page, results: [
    { booking_id, route, journey_date, seat_numbers[], status, total_amount, ticket_url }
  ]
}
```

---

## 6.3 Cancel Booking

```
POST /api/v1/bookings/{id}/cancel
Auth: Bearer <UserToken>
Body: { cancellation_reason? }

200 OK → { message: "Booking cancelled", refund_status: "initiated" }
400 Bad Request → { error: "Cancellation window closed (< 2 hours to departure)" }
404 Not Found   → booking not found or not owned by user
```

> **Phase 1:** Refund is dummy — update `payments.payment_status = 'refunded'`, set `refund_amount`, `refund_initiated_at`.
> **Phase 2:** Call Stripe Refund API with `stripe_payment_id` before updating DB.

---

# 7. Phase 1 — Operator Module

## 7.1 Operator Registration

```
POST /api/v1/auth/operator/register
Body: { company_name, email, phone, gst_number?, address?, password }
201 Created → { message: "Registration submitted. Awaiting admin approval." }
```

---

## 7.2 Add New Bus

```
POST /api/v1/buses
Auth: Bearer <OperatorToken>
Body:
{
  registration_number, bus_type, total_seats,
  route_id, departure_time, arrival_time,
  boarding_location, dropping_location,
  price_per_seat, amenities: ["WiFi", "USB"],
  seat_layout: { rows: 10, cols: 4, layout: [ { seat_number, row, col, type } ] }
}
201 Created → { bus_id, message: "Bus added successfully" }
```

### Post-Creation: Auto-Generate Seats
- On bus creation, the service iterates `seat_layout.layout` and bulk-inserts rows into the `seats` table
- Seat generation is part of the same transaction as bus creation (rollback if layout is invalid)
- `total_seats` must equal `seat_layout.layout.length` — validated before insert

---

## 7.3 Update Bus

```
PUT /api/v1/buses/{id}
Auth: Bearer <OperatorToken>  (must own the bus)
Rules:
  - registration_number is immutable
  - price changes only affect new bookings
  - reducing total_seats blocked if booked_count > new_count
```

---

## 7.4 Bus Status Control

```
PATCH /api/v1/buses/{id}/status
Auth: Bearer <OperatorToken>
Body: { status: "active" | "maintenance" | "removed" }

Status = 'removed':
  → Auto-cancel all future confirmed bookings for this bus
  → Trigger refund process for each affected booking
  → Queue user notifications (service_disruption email)
```

---

## 7.5 View Bookings (Operator)

```
GET /api/v1/operator/bookings
Auth: Bearer <OperatorToken>
Query: ?bus_id=&journey_date=&status=&page=1&page_size=20

200 OK →
{
  total, results: [
    { booking_id, journey_date, bus_registration,
      passengers: [ { name, age, gender, seat_number } ],
      total_amount, status }
  ]
}

GET /api/v1/operator/bookings/export?bus_id=&journey_date=
Response: text/csv  (Phase 2 feature)
```

---

# 8. Phase 1 — Admin Module

## 8.1 Operator Management

| Method | Endpoint | Purpose |
|---|---|---|
| GET | /api/v1/admin/operators | List operators, filter by status |
| GET | /api/v1/admin/operators/{id} | Get operator detail + bus list |
| PATCH | /api/v1/admin/operators/{id} | Approve / Reject / Disable operator |

```
PATCH /api/v1/admin/operators/{id}
Auth: Bearer <AdminToken>
Body: { action: "approve" | "reject" | "disable", reason? }

approve → status = 'approved', approved_by = admin_id, approved_at = NOW()
reject  → status = 'rejected', rejection_reason = reason
disable → status = 'disabled'
         + auto-cancel all future bookings for operator's buses
         + trigger refund queue for affected bookings
         + queue operator_disabled notification emails to all affected users
```

---

## 8.2 Route Management

| Method | Endpoint | Purpose |
|---|---|---|
| GET | /api/v1/admin/routes | List all routes |
| POST | /api/v1/admin/routes | Create new route |
| PUT | /api/v1/admin/routes/{id} | Update route details |
| PATCH | /api/v1/admin/routes/{id}/toggle | Activate / deactivate route |

```
POST /api/v1/admin/routes
Body: { source_city, destination_city, distance_km?, estimated_hours? }
201 Created → { route_id }
409 Conflict → route with same source-destination already exists
```

---

## 8.3 Revenue Dashboard

```
GET /api/v1/admin/dashboard
Auth: Bearer <AdminToken>
Query: ?from=2026-01-01&to=2026-04-30

200 OK →
{
  total_bookings: 1243,
  total_revenue: 1587650.00,
  cancellation_rate: 4.2,
  seat_occupancy_rate: 78.5,
  revenue_by_operator: [ { operator_id, name, revenue } ],
  revenue_by_route: [ { route_id, source, destination, revenue } ],
  bookings_by_date: [ { date, count, revenue } ]   // for chart
}
```

---

# 9. Phase 2 — Stripe Payment Integration

## 9.1 Payment Intent Flow

- Client calls `POST /api/v1/payments/intent` — server creates Stripe PaymentIntent, returns `client_secret`
- Angular confirms payment using Stripe.js (`stripe.confirmCardPayment` or `confirmUpiPayment`)
- Stripe sends `payment_intent.succeeded` webhook to `POST /api/v1/payments/webhook`
- Backend verifies webhook signature using `Stripe-Signature` header, then creates Booking atomically

```
POST /api/v1/payments/intent
Auth: Bearer <UserToken>
Body: { amount, currency: "inr", payment_method: "stripe_card" | "stripe_upi" }
200 OK → { client_secret: "pi_3Px...secret_...", payment_intent_id }

POST /api/v1/payments/webhook   // Stripe webhook — no auth, signature verified
Header: Stripe-Signature: t=...,v1=...
Body: Stripe Event JSON

On event type payment_intent.succeeded:
  → Retrieve pending booking context from session / Redis
  → Create booking + booking_details + payment record (stripe_payment_id stored)
  → Generate PDF ticket async
  → Queue booking_confirmed email notification
```

> **Idempotency:** Stripe webhooks can be delivered more than once. Check if booking already exists for `stripe_payment_id` before creating. Return `200 OK` either way so Stripe stops retrying.

---

## 9.2 Stripe Refund on Cancellation

```
On POST /api/v1/bookings/{id}/cancel (Phase 2):

  1. Retrieve payment record → stripe_payment_id
  2. Call Stripe Refund API: stripe.Refunds.CreateAsync({ PaymentIntent = id })
  3. On success: update payments.stripe_refund_id, refund_amount, refund_initiated_at
  4. Update booking.status = 'cancelled', cancelled_at = NOW()
  5. Queue booking_cancelled notification email
```

---

# 10. Phase 2 — Email Notifications

## 10.1 Notification Queue Pattern

Notifications are written to the `notifications` table synchronously as part of the main transaction, then processed asynchronously by a background Hangfire job that polls every 30 seconds.

| Trigger Event | Recipient | Type | Content Summary |
|---|---|---|---|
| Booking confirmed | User | booking_confirmed | Route, date, seats, total, PDF attachment |
| Booking cancelled | User | booking_cancelled | Cancellation confirmation + refund details |
| Operator approved | Operator | operator_approved | Access granted, login link |
| Operator rejected | Operator | operator_rejected | Rejection reason |
| Operator disabled | Users (affected) | operator_disabled | Service disruption + refund notice + alternate buses |
| Bus removed | Users (affected) | bus_removed | Booking cancellation + refund notice |
| Password reset | User | password_reset | Single-use reset link (30 min TTL) |

---

## 10.2 Notification Worker

```sql
-- Hangfire RecurringJob: every 30 seconds

SELECT TOP 50 * FROM notifications
WHERE status = 'pending' ORDER BY created_at ASC
FOR UPDATE SKIP LOCKED   -- prevent duplicate sends in scaled environment

For each notification:
  Try: send via SendGrid API
    → status = 'sent', sent_at = NOW()
  Catch: status = 'failed'  (retry on next cycle up to 3 attempts)
```

---

# 11. PDF Ticket Generation (QuestPDF)

## 11.1 Ticket Contents

| Field | Source |
|---|---|
| Booking Reference (UUID) | bookings.id |
| Operator Name | operators.company_name |
| Bus Registration Number | buses.registration_number |
| Route: Source → Destination | routes.source_city / destination_city |
| Journey Date & Departure Time | bookings.journey_date + buses.departure_time |
| Boarding Point | buses.boarding_location |
| Passenger Names + Seat Numbers | booking_details rows |
| Total Amount Paid | bookings.total_amount |
| QR Code (Phase 2) | Encodes booking UUID |

## 11.2 Storage & Access

- Generated PDF saved to `/tickets/{booking_id}.pdf` on server (or Azure Blob Storage in production)
- Path stored in `bookings.ticket_pdf_path`
- Download endpoint: `GET /api/v1/bookings/{id}/ticket`
- Endpoint validates that requesting user owns the booking before serving file

---

# 12. API Endpoint Master Reference

## 12.1 Auth Endpoints

| Method | Endpoint | Auth | Phase |
|---|---|---|---|
| POST | /api/v1/auth/register | Public | 1 |
| POST | /api/v1/auth/login | Public | 1 |
| POST | /api/v1/auth/operator/register | Public | 1 |
| POST | /api/v1/auth/operator/login | Public | 1 |
| POST | /api/v1/auth/admin/login | Public | 1 |
| POST | /api/v1/auth/forgot-password | Public | 1 |
| POST | /api/v1/auth/reset-password | Public | 1 |
| POST | /api/v1/auth/logout | User / Op / Admin | 1 |

## 12.2 Bus & Search Endpoints

| Method | Endpoint | Auth | Phase |
|---|---|---|---|
| GET | /api/v1/buses/search | Public | 1 |
| GET | /api/v1/buses/{id} | Public | 1 |
| POST | /api/v1/buses | Operator | 1 |
| PUT | /api/v1/buses/{id} | Operator | 1 |
| PATCH | /api/v1/buses/{id}/status | Operator / Admin | 1 |
| GET | /api/v1/buses/routes/autocomplete | Public | 1 |

## 12.3 Seat & Booking Endpoints

| Method | Endpoint | Auth | Phase |
|---|---|---|---|
| POST | /api/v1/seats/lock | User | 1 |
| DELETE | /api/v1/seats/lock | User | 1 |
| POST | /api/v1/bookings | User | 1 |
| GET | /api/v1/bookings | User | 1 |
| GET | /api/v1/bookings/{id} | User | 1 |
| POST | /api/v1/bookings/{id}/cancel | User | 1 |
| GET | /api/v1/bookings/{id}/ticket | User | 1 |

## 12.4 Operator Dashboard Endpoints

| Method | Endpoint | Auth | Phase |
|---|---|---|---|
| GET | /api/v1/operator/bookings | Operator | 1 |
| GET | /api/v1/operator/bookings/export | Operator | 2 |
| GET | /api/v1/operator/revenue | Operator | 2 |

## 12.5 Admin Endpoints

| Method | Endpoint | Auth | Phase |
|---|---|---|---|
| GET | /api/v1/admin/operators | Admin | 1 |
| GET | /api/v1/admin/operators/{id} | Admin | 1 |
| PATCH | /api/v1/admin/operators/{id} | Admin | 1 |
| GET | /api/v1/admin/routes | Admin | 1 |
| POST | /api/v1/admin/routes | Admin | 1 |
| PUT | /api/v1/admin/routes/{id} | Admin | 1 |
| PATCH | /api/v1/admin/routes/{id}/toggle | Admin | 1 |
| GET | /api/v1/admin/buses | Admin | 1 |
| GET | /api/v1/admin/dashboard | Admin | 1 |
| GET | /api/v1/admin/bookings | Admin | 1 |

## 12.6 Payment Endpoints

| Method | Endpoint | Auth | Phase |
|---|---|---|---|
| POST | /api/v1/payments/intent | User | 2 |
| POST | /api/v1/payments/webhook | Stripe (signature) | 2 |

---

# 13. Error Response Standard

All error responses follow a consistent envelope to simplify Angular error handling.

```json
{
  "status": 400,
  "error": "VALIDATION_ERROR",
  "message": "One or more fields failed validation",
  "details": [
    { "field": "email", "message": "Email already registered" }
  ],
  "trace_id": "3f8a1b2c-..."
}
```

| HTTP Status | Error Code | When Used |
|---|---|---|
| 400 | VALIDATION_ERROR | FluentValidation failures — field-level details |
| 401 | UNAUTHORIZED | Missing or expired JWT token |
| 403 | FORBIDDEN | Role mismatch or account suspended / not approved |
| 404 | NOT_FOUND | Resource does not exist or not owned by caller |
| 409 | CONFLICT | Seat already locked, email already registered, duplicate route |
| 422 | BUSINESS_RULE | Cancel window closed, seat count reduction blocked |
| 429 | RATE_LIMITED | Too many auth attempts |
| 500 | INTERNAL_ERROR | Unexpected server failure — log trace_id, generic message to client |

---

# 14. Non-Functional Implementation Notes

## 14.1 Performance Targets

| Metric | Target | Implementation Approach |
|---|---|---|
| Search API | < 500ms p99 | Composite index (route_id, status) + response caching 30s |
| Seat lock | < 200ms | SELECT FOR UPDATE inside transaction + idx_seat_locks_expiry |
| PDF generation | < 5s | Async post-commit — booking confirmed instantly, PDF follows |
| Concurrent users | 500+ | Stateless API + connection pooling (Npgsql min 10, max 50) |

## 14.2 Security Checklist

- All secrets in environment variables — never in `appsettings.json`
- HTTPS enforced — HTTP → HTTPS redirect in middleware
- CORS: `AllowSpecificOrigin` only (Angular origin + Stripe webhook)
- Parameterized queries via EF Core — zero raw SQL with string interpolation
- Bcrypt cost factor ≥ 12 for all password hashes
- JWT validation: issuer, audience, lifetime, signature — all checked
- Stripe webhook: validate `Stripe-Signature` header before processing
- Admin Hangfire dashboard protected by Admin role policy

## 14.3 Database Indexing Summary

| Table | Index | Purpose |
|---|---|---|
| buses | idx_buses_route_status (route_id, status) | Fast search query join |
| seats | idx_seats_bus_active (bus_id, is_active) | Seat count per bus |
| seat_locks | idx_seat_locks_expiry (seat_id, journey_date, lock_expiry) | Lock validation + cleanup |
| bookings | idx_bookings_user_status (user_id, status) | Booking history per user |
| operators | idx_operators_status (status) | Admin operator filter |
| users | idx_users_status (status) | Active user lookup |

---

# 15. Development Checklist by Phase

## Phase 1 Checklist

| # | Task | Layer | Priority |
|---|---|---|---|
| 1 | Solution structure + EF Core DbContext + migrations | Infrastructure | P0 |
| 2 | JWT auth middleware + RBAC policies | API | P0 |
| 3 | User / Operator / Admin registration & login | Application | P0 |
| 4 | Routes seeding (admin creates) + bus creation with seat generation | Application | P0 |
| 5 | Bus search endpoint + seat status derivation | Application | P0 |
| 6 | Seat locking with SELECT FOR UPDATE SKIP LOCKED | Application | P0 |
| 7 | SignalR SeatAvailabilityHub + Angular client integration | API / Frontend | P0 |
| 8 | Hangfire seat lock expiry cleanup job + SignalR broadcast | Infrastructure | P0 |
| 9 | Booking creation atomic transaction | Application | P0 |
| 10 | Dummy payment handler | Application | P0 |
| 11 | QuestPDF ticket generation | Infrastructure | P1 |
| 12 | Booking cancellation + dummy refund | Application | P1 |
| 13 | Admin operator approve/reject/disable + cascade effects | Application | P1 |
| 14 | Operator booking monitoring dashboard | Application | P1 |
| 15 | User profile read + edit | Application | P2 |

## Phase 2 Checklist

| # | Task | Layer | Priority |
|---|---|---|---|
| 16 | Stripe PaymentIntent creation + Angular Stripe.js confirmation | Application | P0 |
| 17 | Stripe webhook handler (idempotent booking creation) | API | P0 |
| 18 | Stripe Refund API on cancellation | Application | P0 |
| 19 | SendGrid email integration for all notification types | Infrastructure | P1 |
| 20 | Notification background worker (Hangfire) | Infrastructure | P1 |
| 21 | QR code on PDF ticket | Infrastructure | P2 |
| 22 | Operator CSV export | Application | P2 |
| 23 | Admin revenue charts + breakdown | Application | P2 |

## Phase 3 Checklist

| # | Task | Layer | Priority |
|---|---|---|---|
| 24 | Google Maps live tracking integration | Application | P1 |
| 25 | User wallet / credits system | Application | P1 |
| 26 | Coupon & discount code engine | Application | P1 |
| 27 | Ratings & reviews for operators/buses | Application | P2 |
| 28 | PostgreSQL read replica for search scaling | Infrastructure | P2 |
| 29 | Angular PWA / mobile app | Frontend | P3 |

---

*Bus Booking System — Backend Implementation Guide v1.0 | April 2026*
```