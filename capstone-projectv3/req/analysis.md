
---

## What's Well-Built ✅

| Area | Highlights |
|---|---|
| **Architecture** | Clean 4-layer separation (Controller → Service → Repository → DB). Controllers are intentionally thin — zero business logic |
| **Shipment State Machine** | Explicit `ValidDriverTransitions` dictionary — prevents skipping steps or setting invalid statuses |
| **Race Condition Prevention** | `SELECT ... FOR UPDATE` (pessimistic locking) on driver self-assign, so two drivers can't grab the same shipment |
| **OTP Security** | `RandomNumberGenerator` (cryptographic, not `System.Random`), code cleared from DB after use, never returned to driver |
| **Real-Time Layer** | Clean `TrackingHub` ↔ `TrackingService` split; targeted OTP pushes (per-connection) vs. group broadcasts properly separated |
| **GPS Simulation** | Proper `IServiceScopeFactory` pattern (singleton using scoped DbContext), Haversine formula, bulk `ExecuteUpdateAsync` |
| **Exception Hierarchy** | Custom `AppException` hierarchy → centralized middleware mapping to correct HTTP codes |
| **Data Design** | 1NF child tables for addresses/items, append-only event log, vertical partitioning for profiles |

---

## Key Flaws 🔴🟡

| Severity | Issue |
|---|---|
| 🔴 Critical | **Delivery OTP goes to the Sender, not the actual Recipient** — `PushOtpToRecipientAsync` uses `shipment.CustomerId` (sender) instead of a recipient user ID |
| 🔴 Critical | **`RateLimitException` returns HTTP 400 instead of 429** — OTP lockout looks like a bad request |
| 🔴 Critical | **`hasActiveDelivery` computed but never used** — admin can suspend a driver mid-delivery, leaving shipment in broken state |
| 🔴 Critical | **Hardcoded DB password + JWT key** in `appsettings.json` |
| 🔴 Critical | **Admin seeder fully commented out** — no way to create an admin user out of the box |
| 🟡 Medium | `SavedAddress` mapped as `1:1` in fluent API but is actually `1:Many` |
| 🟡 Medium | `UpdateSavedAddressAsync` calls `UpdateAsync(profile)` instead of `UpdateAsync(address)` |
| 🟡 Medium | Dashboard runs 7 separate `COUNT` queries — should be one |
| 🟡 Medium | `GetShipmentByTrackingNumberAsync` and `GetInTransitShipmentsAsync` are dead code |
| 🟡 Medium | No pagination size cap (`size=10000` would load 10k rows) |
| 🟡 Medium | `BroadcastOtpRegeneratedAsync` is implemented but never called in `RegenerateOtpAsync` |
| 🟢 Minor | `Console.WriteLine` debug calls left in production code |
| 🟢 Minor | 400+ lines of commented-out old code in `ShipmentService.cs` |
| 🟢 Minor | `GetUserId()` duplicated across 4 controllers — needs a base controller |