Based on your SRS, here's the complete file list for Services, Repositories, and Interfaces:

---

## Interfaces

**`/Interfaces/IAuthService.cs`**
**`/Interfaces/IShipmentService.cs`**
**`/Interfaces/IDriverService.cs`**
**`/Interfaces/IAdminService.cs`**
**`/Interfaces/IOtpService.cs`**
**`/Interfaces/ITrackingNumberService.cs`**

**`/Interfaces/IUserRepository.cs`**
**`/Interfaces/IShipmentRepository.cs`**
**`/Interfaces/IDriverRepository.cs`**
**`/Interfaces/IRefreshTokenRepository.cs`**

---

## Services

**`/Services/AuthService.cs`** — register, login, refresh token rotation, logout

**`/Services/ShipmentService.cs`** — create shipment, list own, get by ID, queue (driver), assign, status transitions (PickedUp → InTransit → Arrived), state machine guard

**`/Services/OtpService.cs`** — generate 4-digit OTP, request-pickup-otp, verify-pickup-otp, request-delivery-otp, verify-delivery-otp, regenerate-otp, attempt count enforcement

**`/Services/DriverService.cs`** — get own profile, toggle op-status

**`/Services/AdminService.cs`** — list all drivers, get driver detail, update driver account status, list all shipments, override shipment status, dashboard metrics

**`/Services/TrackingNumberService.cs`** — generate unique TRK-XXXXXX, collision retry logic

---

## Repositories

**`/Repositories/UserRepository.cs`** — get by ID, get by email, create, update

**`/Repositories/ShipmentRepository.cs`** — create, get by ID, get by tracking number (public), list by customer (paginated), list pending queue (paginated), assign with row lock transaction, update status, list all (admin, filtered + paginated)

**`/Repositories/DriverRepository.cs`** — get by user ID, get all (admin, filtered), update op-status, update account-status, update GPS coordinates

**`/Repositories/RefreshTokenRepository.cs`** — create, get by hash, revoke, delete expired

---

## Key Design Notes

- **`OtpService`** is separate from `ShipmentService` because OTP logic (generation, validation, expiry, attempt count, regeneration) is complex enough to isolate and test independently.

- **`TrackingNumberService`** is separate so `ShipmentService` doesn't carry that concern and the generator is easily unit-testable.

- **`ShipmentRepository`** handles the self-assign with a pessimistic row lock — this belongs in the repository, not the service, since it's a DB-level concern. The service calls it inside a try-catch for the 409 response.

- **No `ShipmentEventRepository`** — events are always inserted as part of another operation's transaction (status change, OTP verify, override). They never need a standalone query interface, so `ShipmentRepository` handles inserting events inline.

- **No `ShipmentItemRepository`** — items are always created with the parent shipment and never queried independently. EF Core navigation properties handle cascade load.

- The `GpsSimulationService` (BackgroundService) is **not** in this layer — it lives in `/BackgroundServices/GpsSimulationService.cs` and depends on `IDriverRepository` and `IHubContext`.