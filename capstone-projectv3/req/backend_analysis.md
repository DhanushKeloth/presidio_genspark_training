# ShipmentTrackingAPI — Complete Backend Analysis

> **Stack:** ASP.NET Core (.NET), Entity Framework Core, PostgreSQL, SignalR, JWT Auth  
> **Architecture:** Layered — Controller → Service → Repository → DbContext

---

## 1. Architecture Overview

```
HTTP/WS Clients
      │
      ▼
Controllers (thin — only extracts JWT claims, delegates to services)
      │
      ▼
Services (all business logic lives here)
      │
      ├── Repositories (EF Core LINQ + raw SQL where EF can't express it)
      │         │
      │         └── AppDbContext (EF Core + Npgsql, PostgreSQL enums)
      │
      └── TrackingService (singleton) ──► SignalR TrackingHub ──► Clients
                │
      GpsSimulationService (hosted background service, runs every 5s)
```

### Dependency Injection Lifetimes

| Service | Lifetime | Reason |
|---|---|---|
| All Repositories | Scoped | One per HTTP request |
| ShipmentService, OtpService, AdminService, CustomerService, AuthService, DriverService | Scoped | Depend on scoped DbContext |
| TrackingService | **Singleton** | Holds the in-memory `ConcurrentDictionary` of `userId → connectionId` — must survive across requests |
| GpsSimulationService | Singleton (hosted) | Long-running background task |
| IPasswordHasher\<User\> | Scoped | Standard Identity |

---

## 2. Database Design

### Tables

| Table | Purpose |
|---|---|
| `users` | Central identity — all roles share one table. `citext` email for case-insensitive uniqueness. Soft-delete via `is_active`. |
| `customer_profiles` | 1:1 with `users` for Customer-specific fields (phone, alternate phone, profile image) |
| `driver_profiles` | 1:1 with `users` for Driver-specific fields (vehicle, license, GPS, account/op status) |
| `saved_addresses` | Customer's address book. Partial unique index enforces at-most-one default per customer. |
| `shipments` | Core delivery contract. Has timestamp fields: `picked_up_at`, `delivered_at`, `cancelled_at`, `failed_at`. |
| `shipment_addresses` | Child table: one Pickup + one Dropoff row per shipment (replaces 8 nullable columns on shipments). |
| `shipment_items` | Child table: one row per item type per booking (1NF). |
| `shipment_otp_windows` | OTP state — one row per (shipment, otp_type). Stores code, expiry, attempt count, verified_at. |
| `shipment_events` | Append-only audit log. One row per state transition. Never updated or deleted. |

### PostgreSQL Enums (mapped via Npgsql)

| Enum | Values |
|---|---|
| `user_role` | Customer, Driver, Admin |
| `driver_account_status` | PendingApproval, Active, Suspended, Deleted |
| `driver_op_status` | Available, InTransit, Offline |
| `shipment_status` | Pending, Assigned, PickedUp, InTransit, Arrived, Delivered, Cancelled, FailedDelivery |
| `address_type` | Pickup, Dropoff |
| `otp_type` | Pickup, Delivery |

### Key Design Decisions
- **citext extension** — Email stored as `citext` so `user@mail.com` and `USER@MAIL.COM` are treated as duplicates at DB level.
- **pgcrypto extension** — Declared but not actively used in application code.
- **Vertical partitioning** — `users` table only holds identity; profile-specific columns live in role-specific tables. Prevents nullable columns for all other roles.
- **Append-only event log** — `shipment_events` is never updated/deleted — it's the historical audit trail.

---

## 3. Feature Implementation Details

### 3.1 Authentication (`AuthController` + `AuthService`)

**Endpoints:**
- `POST /api/auth/register/customer`
- `POST /api/auth/register/driver`
- `POST /api/auth/login`

**Customer Registration Flow:**
1. Checks if email already exists → `ConflictException` (409)
2. Creates a `User` row with `Role = Customer`
3. Hashes password using `ASP.NET Core PasswordHasher<User>` (PBKDF2)
4. Creates linked `CustomerProfile` row in the same `SaveAsync`
5. Returns `RegisterResponseDto` (userId, email, role, message)

**Driver Registration Flow:**
1. Checks email uniqueness → `ConflictException`
2. Checks license number uniqueness → `ConflictException`
3. Creates `User` + `DriverProfile` in one save
4. Driver starts with `AccountStatus = PendingApproval` — cannot login until admin approves

**Login Flow:**
1. Fetches user by email
2. Verifies password with `PasswordHasher.VerifyHashedPassword`
3. For drivers: checks `AccountStatus` (blocks PendingApproval, Suspended, Deleted)
4. For customers/admins: checks `IsActive`
5. Generates a signed JWT (HMAC-SHA256, 8-hour expiry)

**JWT Claims included:**
- `sub` → userId (int)
- `email` → user's email
- `role` → UserRole string (Customer/Driver/Admin)

---

### 3.2 Shipment Lifecycle (`ShipmentController` + `ShipmentService`)

#### State Machine

```
Pending ──[Driver Assign]──► Assigned ──[Pickup OTP Verified]──► PickedUp
                                                                      │
                                                              [Start Transit]
                                                                      │
                                                                  InTransit
                                                                      │
                                                             [Arrived at Dest]
                                                                      │
                                                                   Arrived
                                                                   /    \
                                              [Delivery OTP Verified]  [Fail Delivery]
                                                        │                      │
                                                   Delivered            FailedDelivery
Any State ──[Customer Cancel / Admin Override]──► Cancelled
```

**Only two transitions go through `UpdateStatusAsync`:**
- `PickedUp → InTransit`
- `InTransit → Arrived`

All others have dedicated service methods (OTP verification, `AssignDriverAsync`, `CancelShipmentAsync`, `FailDeliveryAsync`).

#### Book Shipment (`POST /api/shipments`)
- **Role:** Customer only
- Idempotency guard: same customer + same pickup+dropoff within 60 seconds → 409
- Generates unique tracking number: `TRK-XXXXXX` (6 hex chars from `Guid.NewGuid()`)
- Uses a DB transaction to atomically create: `Shipment` + 2 `ShipmentAddress` rows + N `ShipmentItem` rows + initial `ShipmentEvent`
- Returns `201 Created` with `TrackingNumber`

#### Get My Shipments (`GET /api/shipments`)
- **Role:** Customer only
- Paginated response with optional `?status=` filter
- Returns area-level addresses (no customer PII exposed)

#### Get Shipment Detail (`GET /api/shipments/{id}`)
- **Role:** Customer, Driver, Admin
- Full detail: addresses, items, event timeline, driver info
- Ownership enforced: Customer sees only own shipments; Driver sees only assigned ones; Admin sees all

#### Cancel Shipment (`DELETE /api/shipments/{id}`)
- **Role:** Customer only
- Only cancellable when `Status = Pending` (before any driver claims)
- Broadcasts `StatusUpdated` to SignalR group after save

#### Public Track (`GET /api/track/{trackingNumber}`)
- **Role:** Anonymous (no JWT required)
- Single-query fetch with all includes
- Driver GPS exposed ONLY if status is `InTransit`
- OTP codes and recipient phone NEVER exposed

#### Driver: Get Pending Queue (`GET /api/shipments/queue`)
- **Role:** Driver only
- FIFO order (oldest Pending first)
- Only shows public-safe fields (no customer PII)

#### Driver: Assign Shipment (`PUT /api/shipments/{id}/assign`)
- **Race condition protection:** uses `SELECT ... FOR UPDATE` (raw SQL pessimistic lock)
- Two drivers simultaneously trying to grab the same job → second sees Status=Assigned → 409

#### Driver: Update Status (`PUT /api/shipments/{id}/status`)
- **Explicit state machine:** `ValidDriverTransitions` dictionary — only `PickedUp→InTransit` and `InTransit→Arrived` are accepted
- Terminal statuses (Delivered, Cancelled, FailedDelivery) explicitly rejected — must use their dedicated endpoints
- Broadcasts `StatusUpdated` and (for Arrived) `DriverArrived` to SignalR group

#### Driver: Fail Delivery (`POST /api/shipments/{id}/fail-delivery`)
- Guard: `Status` must be `Arrived` — driver must be at destination
- Broadcasts `StatusUpdated (FailedDelivery)` via SignalR

---

### 3.3 OTP Chain of Custody (`OtpService`)

The dual-OTP system ensures proof of pickup (POP) and proof of delivery (POD).

**Security rules:**
- OTP generated using `RandomNumberGenerator.GetInt32(0, 10_000)` (cryptographic — never `System.Random`)
- 4-digit numeric code, zero-padded (e.g., `0042`)
- Max 3 attempts; 4th → `RateLimitException` (429)
- Code cleared from DB after successful verification (replay protection)
- **Code is NEVER returned to the Driver in any API response** — pushed via SignalR to Sender/Recipient only
- 15-minute expiry per window
- Regeneration only allowed if locked (3 failed) or expired

**Pickup OTP Flow:**
1. Driver calls `POST /api/shipments/{id}/request-pickup-otp` (shipment must be `Assigned`)
2. `OtpService` generates code, upserts `ShipmentOtpWindow` row
3. Code is pushed via SignalR directly to the Sender's connection (not broadcast to group)
4. Driver verbally asks Sender for the code
5. Driver calls `POST /api/shipments/{id}/verify-pickup-otp` with the code
6. On success: shipment → `PickedUp`, `picked_up_at` set, OTP code cleared, event logged

**Delivery OTP Flow** (mirrors Pickup, but at `Arrived` status, pushed to Recipient):
1. Driver calls `POST /api/shipments/{id}/request-delivery-otp`
2. Code pushed to Recipient's SignalR connection
3. Driver calls `POST /api/shipments/{id}/verify-delivery-otp`
4. On success: shipment → `Delivered`, `delivered_at` set, `ShipmentDelivered` broadcast to group

**OTP Upsert:** Uses raw PostgreSQL `INSERT ... ON CONFLICT ... DO UPDATE` because EF Core doesn't support native upsert on non-PK unique constraints.

---

### 3.4 Real-Time Tracking — SignalR (`TrackingHub` + `TrackingService`)

**Architecture split:**
- `TrackingHub` — handles **client → server** events: `JoinShipmentGroup`, `LeaveShipmentGroup`, connection lifecycle
- `TrackingService` — handles **server → client** broadcasts: all pushes go through here

**Group model:** `shipment-{trackingNumber}` (e.g., `shipment-TRK-A3X9B1`)

**Connection registry:**
- `TrackingService` maintains two `ConcurrentDictionary`:
  - `userId → connectionId` (for targeted OTP pushes)
  - `connectionId → userId` (for cleanup on disconnect)
- One connectionId per user — if user opens a new tab, latest tab wins

**JWT auth for SignalR:** Token is read from query string (`?access_token=...`) because browsers cannot set `Authorization` headers for WebSocket connections. Configured in `JwtBearerEvents.OnMessageReceived`.

**Events pushed to clients:**

| Event | Audience | Trigger |
|---|---|---|
| `LocationUpdated` | Group | GpsSimulationService every 5s (InTransit only) |
| `StatusUpdated` | Group | Any shipment state transition |
| `DriverArrived` | Group | Driver transitions to Arrived status |
| `ShipmentDelivered` | Group | Delivery OTP verified |
| `PickupOtpGenerated` | Sender's connection only | Pickup OTP requested |
| `DeliveryOtpGenerated` | Recipient's connection only | Delivery OTP requested |
| `OtpRegenerated` | Group | OTP regenerated (no code exposed) |

**On `JoinShipmentGroup`:** Hub sends a `StatusUpdated` ack with `NewStatus = "Connected"` so Angular knows the join succeeded.

---

### 3.5 GPS Simulation (`GpsSimulationService`)

- Runs every 5 seconds (configurable via `GpsSimulation:TickIntervalSeconds`)
- Queries only `InTransit` shipments (partial index keeps this fast)
- Uses linear interpolation (`Lerp`) to move driver's GPS pin toward dropoff
- `_stepFraction = 0.05` → moves 5% of remaining distance per tick
- Haversine formula for real-world distance check; stops moving pin when within 50m
- Uses `ExecuteUpdateAsync` (bulk SQL UPDATE) — never loads full entity for GPS write
- Resolves `AppDbContext` per tick via `IServiceScopeFactory` (correct pattern for singleton using scoped service)
- All DB writes done first, then all SignalR broadcasts — avoids partial broadcast

---

### 3.6 Admin Features (`AdminController` + `AdminService`)

**All endpoints require `[Authorize(Roles = "Admin")]`.**

| Endpoint | Description |
|---|---|
| `GET /api/admin/drivers?status=PendingApproval` | Paginated list of drivers, filterable by account status |
| `GET /api/admin/drivers/{id}` | Full driver profile detail |
| `PUT /api/admin/drivers/status` | Change driver account status (Approve, Suspend, Delete) |
| `GET /api/admin/shipments?status=InTransit` | Paginated list of all shipments, filterable by status or driverId |
| `PUT /api/admin/shipments/{id}/override-status` | Bypass state machine and force any status change |
| `GET /api/admin/dashboard` | Aggregated metrics |

**Driver status transitions by Admin:**
- `Active` → sets `ApprovedBy` (admin ID) and `ApprovedAt`
- `Suspended` or `Deleted` → clears `OpStatus` to null
- `Deleted` → also sets `user.IsActive = false` (soft delete on users table)

> ⚠️ `UpdateDriverAccountStatusAsync` queries `hasActiveDelivery` but **never uses the result** — silently allows suspending/deleting a driver mid-delivery.

**Dashboard metrics** — runs 7 separate `CountAsync` queries against the DB (one per metric). Not efficient for high traffic.

**Admin Override:** Logs an audit event with the admin's userId, previous status, new status, and reason. Also resets driver `OpStatus` to Available when a terminal status is set.

---

### 3.7 Customer Profile & Address Book (`CustomerController` + `CustomerService`)

**All endpoints require `[Authorize(Roles = "Customer")]`.**

| Endpoint | Description |
|---|---|
| `GET /api/customer/profile` | Returns combined profile (users + customer_profiles) |
| `PUT /api/customer/profile` | Partial update of contact fields only (not email/name) |
| `GET /api/customer/addresses` | List all saved addresses |
| `POST /api/customer/addresses` | Add new address; if `IsDefault=true`, clears previous default atomically |
| `PUT /api/customer/addresses/{id}` | Update address; ownership enforced at query level |
| `DELETE /api/customer/addresses/{id}` | Hard delete; returns 404 (not 403) if not found (security by design — no enumeration) |

---

### 3.8 Driver Profile & Operational Status (`DriverController` + `DriverService`)

**All endpoints require `[Authorize(Roles = "Driver")]`.**

| Endpoint | Description |
|---|---|
| `GET /api/driver/profile` | Returns own driver profile |
| `PUT /api/driver/status` | Toggle op status: Available / Offline |

**Guards on op status toggle:**
1. Account must be `Active`
2. Cannot self-set `InTransit` (set automatically by ShipmentService)
3. Cannot go `Available` while holding an active (non-terminal) shipment

When going `Available`: optionally updates GPS location.  
When going `Offline`: clears GPS coordinates for privacy.

---

### 3.9 Exception Handling (`GlobalExceptionHandlerMiddleware`)

All unhandled exceptions are caught here. Custom domain exceptions map to HTTP status codes:

| Exception | HTTP Status |
|---|---|
| `BadRequestException` | 400 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `NotFoundException` | 404 |
| `ConflictException` | 409 |
| `RateLimitException` | 429 (registered in switch default as 400 — **see Flaws**) |
| All others | 500 |

Exception hierarchy: All custom exceptions inherit `AppException : Exception`.

> ⚠️ **`RateLimitException` returns 400 instead of 429.** The `_` switch default case fires for it, returning `BadRequest`.

---

### 3.10 Input Validation

Two custom `ValidationAttribute` classes:

**`StrongPasswordAttribute`:** Requires password ≥ 8 chars, at least one uppercase, lowercase, digit, and special character (`@$!%*?&`).

**`ValidPhoneNumberAttribute`:** (file present, not analyzed in detail — standard regex phone validation).

Both are applied on DTOs and fire through ASP.NET Core model validation pipeline before the controller action executes.

---

## 4. Identified Flaws & Issues

### 🔴 Critical Issues

#### 1. `RateLimitException` returns HTTP 400, not 429
**Location:** `GlobalExceptionHandlerMiddleware.cs`  
**Issue:** The switch statement's default catches `RateLimitException` and returns `400 Bad Request` instead of `429 Too Many Requests`. The client cannot distinguish a bad OTP attempt from a rate-limit lockout.

```csharp
// Current — RateLimitException falls into default:
_ => (int)HttpStatusCode.BadRequest   // Should be 429
```

#### 2. `hasActiveDelivery` Check in `UpdateDriverAccountStatusAsync` is Computed but Never Used
**Location:** `AdminService.cs` — `UpdateDriverAccountStatusAsync`  
The code queries whether the driver has an active delivery, but **never acts on it**. An admin can suspend or delete a driver who is currently mid-delivery, leaving the shipment in a broken state (driver suspended but shipment still `InTransit`).

```csharp
var hasActiveDelivery = await _ctx.Shipments.AnyAsync(...); // Computed
// ... but no guard: if (hasActiveDelivery) throw new ConflictException(...);
```

#### 3. Hardcoded Credentials in `appsettings.json`
**Location:** `appsettings.json`  
Both the database password and JWT signing key are in plaintext in the committed config file:
```json
"Password=1234"
"Key": "YourSuperSecretUncrackableKeyThatIsAtLeast32CharsLong123!"
```
These should be in environment variables or a secrets manager (User Secrets in dev, Azure Key Vault / AWS Secrets Manager in prod).

#### 4. `DataSeeder.cs` Fully Commented Out
**Location:** `DataSeeder.cs` + `Program.cs`  
The admin seeder is entirely commented out, which means there is no admin user in the database unless manually inserted. The system is unusable out of the box — no admin can approve drivers.

---

### 🟡 Medium Issues

#### 5. `SavedAddress` has a `HasOne ... WithOne` Mapping but it's a `HasMany` Relationship
**Location:** `AppDbContext.cs` line 101  
```csharp
entity.HasOne(d => d.Customer).WithOne(p => p.SavedAddress)...
```
The `CustomerProfile` model should have `ICollection<SavedAddress>` (many addresses per customer), not a single `SavedAddress`. The relationship is `1:Many` but is misconfigured as `1:1` in EF fluent API. This likely causes an EF runtime error or incorrect query behavior.

#### 6. `UpdateSavedAddressAsync` Calls `UpdateAsync(profile)` Instead of `UpdateAsync(address)`
**Location:** `CustomerService.cs` line 182  
```csharp
await _customerRepo.UpdateAsync(profile);  // Should be address!
```
The profile is marked as modified but the address entity changes are not explicitly tracked via `UpdateAsync(address)`. This works because EF change tracking is on, but it's semantically wrong and fragile — if `AsNoTracking()` is ever added to the repo query, it will silently stop saving address changes.

#### 7. `SavedAddress.UpdatedAt` is Commented Out
**Location:** `CustomerService.cs` lines 133 and 180  
```csharp
// address.UpdatedAt = DateTime.UtcNow;
```
The `updated_at` column is never set for saved addresses on update. The audit timestamp will always show the creation time.

#### 8. `CustomerProfileDto.CreatedAt` and `SavedAddressDto.CreatedAt` are Commented Out
**Location:** `CustomerService.cs` lines 222, 237  
These fields exist in the model but are intentionally excluded from DTOs with no explanation. May be intentional, but the inconsistency suggests they were forgotten.

#### 9. Dashboard Uses 7 Separate COUNT Queries
**Location:** `AdminService.cs` — `GetDashboardMetricsAsync`  
Seven independent `CountAsync` calls are made to the database in sequence. This should be a single SQL query with conditional aggregation:
```sql
SELECT 
  COUNT(*) FILTER (WHERE status = 'Pending') as pending,
  COUNT(*) FILTER (WHERE status = 'Assigned') AS assigned, ...
```

#### 10. `DriverProfileDto.ApprovedAt` Commented Out
**Location:** `DriverService.cs` line 140  
```csharp
// ApprovedAt = profile.ApprovedAt,
```
Approval date is available in the model but not exposed in the driver's own profile DTO, even though it's meaningful to the driver.

#### 11. `GetShipmentByTrackingNumberAsync` is Never Called
**Location:** `ShipmentRepository.cs` — `GetShipmentByTrackingNumberAsync` (line 69)  
This method was part of the original implementation but `GetPublicTrackingAsync` was refactored to directly query via `_ctx.Shipments`. The method is dead code now.

#### 12. `GetInTransitShipmentsAsync` is Never Called
**Location:** `ShipmentRepository.cs` — `GetInTransitShipmentsAsync` (line 89)  
`GpsSimulationService` does NOT call this method — it does its own inline query with a projection. This is dead code.

#### 13. `AddControllers()` is Registered Twice
**Location:** `Program.cs` lines 122 and 125  
```csharp
builder.Services.AddControllers();
// ...
builder.Services.AddControllers().AddJsonOptions(...);
```
The second call supersedes the first. The first call is redundant.

#### 14. CORS Policy Allows Only Two Origins (Hardcoded)
**Location:** `Program.cs` line 26  
```csharp
policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:5500")
```
The allowed origins are hardcoded in code. This should be configurable via `appsettings.json`. In production, these addresses would need code changes.

#### 15. `OtpService.PushOtpToRecipientAsync` Pushes to `senderUserId`, Not Actual Recipient
**Location:** `OtpService.cs` lines 96–97  
```csharp
await _tracking.PushOtpToRecipientAsync(shipment.CustomerId,  // ← always the Customer (Sender)!
    shipment.TrackingNumber, code, expiresAt);
```
For the Delivery OTP, the code should be pushed to the **recipient** (the person receiving the parcel), but `shipment.CustomerId` is always the **sender**. There is no `RecipientUserId` on the `Shipment` model. The recipient is not a system user — they are identified only by `ContactName` and `ContactPhone` on the Dropoff address. The OTP delivery mechanism for the recipient is fundamentally broken — the code will go to the sender, not the recipient.

---

### 🟢 Minor / Style Issues

#### 16. `Console.WriteLine` Calls Left in Production Code
**Locations:** `TrackingService.cs` line 107, `TrackingHub.cs` line 158  
Debug `Console.WriteLine` calls should be replaced with `_logger` calls for structured logging.

#### 17. Large Commented-Out Code Block in `ShipmentService.cs`
**Location:** `ShipmentService.cs` lines 1–407  
Over 400 lines of old implementation code is commented out at the top of the file. This is dead code that adds confusion and should be removed.

#### 18. `GetUserId()` is Duplicated Across 3 Controllers
**Location:** `ShipmentController.cs`, `CustomerController.cs`, `DriverController.cs`, `AdminController.cs`  
All four controllers have a near-identical `GetUserId()` private helper that extracts the user ID from JWT claims. This should be extracted into a `BaseApiController` class.

#### 19. Inconsistent Namespace Usage
The middleware is in namespace `ShipmentTrackingAPI.Middleware` but the folder is `Middlewares`. Some services use `ShipmentTrackingAPI.Interfaces` while others use `ShipmentTrackingAPI.Services.Interfaces`. Namespace organization is inconsistent throughout.

#### 20. No Input Validation on Pagination Parameters
**Location:** `ShipmentService.GetCustomerShipmentsAsync` and other paginated methods  
No `size` cap is enforced in the service layer. The controller doc says "Max page size 50" but `size` is never clamped. Passing `size=10000` would load 10,000 rows.

#### 21. `TrackingService.BroadcastOtpRegeneratedAsync` is Never Called
**Location:** `TrackingService.cs` line 159  
`RegenerateOtpAsync` in `OtpService` does NOT call this method. The method exists in `ITrackingService` and is implemented but has no callers. If the intent was to notify the group that an OTP was regenerated (without sending the code), it was forgotten.

#### 22. `AddOpenApi()` and `AddSwaggerGen()` Both Registered
**Location:** `Program.cs` lines 121 and 124  
Both `AddOpenApi()` (ASP.NET Core's new minimal API OpenAPI) and `AddSwaggerGen()` are registered. They serve different purposes and the `MapOpenApi()` call on line 158 adds a third endpoint. This creates three parallel API documentation endpoints, which is redundant.

---

## 5. Feature Coverage Summary

| Feature | Implemented | Notes |
|---|---|---|
| Customer Registration | ✅ | With duplicate email check |
| Driver Registration | ✅ | Starts in PendingApproval |
| JWT Authentication | ✅ | 8-hour, HMAC-SHA256 |
| Admin: Approve/Suspend/Delete Driver | ✅ | But active-delivery guard is a dead variable |
| Admin: Override Shipment Status | ✅ | Bypasses state machine, with audit log |
| Admin: Dashboard Metrics | ✅ | N+1 query pattern |
| Book Shipment | ✅ | Transactional, idempotency guard |
| Cancel Shipment | ✅ | Only while Pending |
| Public Tracking | ✅ | No auth, no PII exposed |
| Driver: View Job Queue | ✅ | Paginated, FIFO |
| Driver: Self-Assign | ✅ | Pessimistic lock (SELECT FOR UPDATE) |
| Driver: State Transitions | ✅ | Explicit state machine |
| Pickup OTP | ✅ | Cryptographic, max 3 attempts |
| Delivery OTP | ⚠️ | OTP pushed to sender, not actual recipient |
| OTP Regeneration | ✅ | On expiry or lockout |
| Real-time GPS Updates | ✅ | SignalR + GpsSimulationService (5s ticks) |
| Real-time Status Updates | ✅ | SignalR group broadcast |
| Targeted OTP Push | ⚠️ | Sender-to-sender for Delivery OTP (bug) |
| Customer Profile CRUD | ✅ | Partial update |
| Customer Address Book | ✅ | Single-default enforced |
| Driver Profile + Op Status | ✅ | With guards |
| Admin Seeding | ❌ | Commented out — no admin user can be created |
| Refresh Token | ❌ | No token refresh — 8-hour tokens only |
| Email/Push Notifications | ❌ | OTP depends purely on SignalR presence |
| Rate Limiting (HTTP) | ❌ | No `AspNetCoreRateLimit` or similar |
| Unit/Integration Tests | ❌ | No test project found |
