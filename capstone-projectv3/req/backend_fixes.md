# Backend Fixes — Critical + Medium Issues
## SwiftParcel / ShipmentTrackingAPI

---

## 🔴 CRITICAL FIXES

---

### Fix 1 — Active Delivery Guard is Dead Code (Issue #1)

**File:** `AdminService.cs`

The variable `hasActiveDelivery` is computed but never used to block the operation.

```csharp
// ❌ Current — guard computed but never throws
var hasActiveDelivery = await _ctx.Shipments.AnyAsync(s =>
    s.DriverId == driverUserId &&
    s.Status != ShipmentStatus.Delivered &&
    s.Status != ShipmentStatus.Cancelled &&
    s.Status != ShipmentStatus.FailedDelivery);

// ... continues to suspend anyway
```

```csharp
// ✅ Fix — actually use the result
var hasActiveDelivery = await _ctx.Shipments.AnyAsync(s =>
    s.DriverId == driverUserId &&
    s.Status != ShipmentStatus.Delivered &&
    s.Status != ShipmentStatus.Cancelled &&
    s.Status != ShipmentStatus.FailedDelivery);

if (hasActiveDelivery && newStatus == DriverAccountStatus.Suspended)
{
    // Warn but allow — Admin makes the call.
    // Return a warning alongside the success response.
    _logger.LogWarning(
        "AdminService: Driver {DriverId} suspended while having an active delivery.",
        driverUserId);
    
    // Set a flag on the response DTO to surface the warning
    result.Warning = "Driver has an active delivery in progress. " +
                     "The shipment will need admin intervention.";
}
```

**Also update your response DTO:**
```csharp
public class UpdateDriverStatusResponseDto
{
    public string Message { get; set; } = default!;
    public string? Warning { get; set; }  // ← add this
}
```

---


### Fix 5 — Delivery OTP Pushed to Sender, Not Recipient (Issue #15)

**File:** `OtpService.cs`

The Recipient in this capstone is not a system user — they have no `userId`.
The correct approach is: **both OTPs go to `shipment.CustomerId`** (the Customer
who booked is the one tracking and managing both OTPs for the capstone).

This is already documented in the SRS Section 2.7.

```csharp
// In RequestOtpAsync — both branches use CustomerId
if (otpType == OtpType.Pickup)
    await _tracking.PushOtpToSenderAsync(
        shipment.CustomerId,
        shipment.TrackingNumber,
        code,
        expiresAt);
else
    await _tracking.PushOtpToRecipientAsync(
        shipment.CustomerId,       // ← CustomerId, not a separate recipientId
        shipment.TrackingNumber,
        code,
        expiresAt);

// In RegenerateOtpAsync — same fix
if (otpType == OtpType.Pickup)
    await _tracking.PushOtpToSenderAsync(
        shipment.CustomerId,
        shipment.TrackingNumber,
        code,
        expiresAt);
else
    await _tracking.PushOtpToRecipientAsync(
        shipment.CustomerId,       // ← CustomerId
        shipment.TrackingNumber,
        code,
        expiresAt);

// Also add the missing BroadcastOtpRegeneratedAsync call in RegenerateOtpAsync:
await _tracking.BroadcastOtpRegeneratedAsync(
    shipment.TrackingNumber,
    otpType.ToString(),
    expiresAt);
```

---

## 🟡 MEDIUM FIXES

---

### Fix 6 — SavedAddress HasOne/WithOne Misconfigured (Issue #5)

**File:** `AppDbContext.cs`

```csharp
// ❌ Current — wrong, treats it as 1:1
entity.HasOne(d => d.Customer).WithOne(p => p.SavedAddress)...

// ✅ Fix — correct 1:Many relationship
entity.HasOne(d => d.Customer)
      .WithMany(p => p.SavedAddresses)   // plural
      .HasForeignKey(d => d.CustomerId)
      .OnDelete(DeleteBehavior.Cascade);
```

**Also fix `CustomerProfile` model:**
```csharp
// ❌ Current
public SavedAddress? SavedAddress { get; set; }

// ✅ Fix
public ICollection<SavedAddress> SavedAddresses { get; set; } = new List<SavedAddress>();
```

---

### Fix 7 — UpdateSavedAddressAsync Updates Wrong Entity (Issue #6)

**File:** `CustomerService.cs`

```csharp
// ❌ Current — updates profile instead of address
await _customerRepo.UpdateAsync(profile);

// ✅ Fix — update the address entity
address.UpdatedAt = DateTime.UtcNow;   // also fix issue #7
await _customerRepo.UpdateAddressAsync(address);
```

---

### Fix 8 — SavedAddress.UpdatedAt Never Set (Issue #7)

**File:** `CustomerService.cs` — both `AddSavedAddressAsync` and `UpdateSavedAddressAsync`

```csharp
// In AddSavedAddressAsync
var address = new SavedAddress
{
    ...
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow,   // ← uncomment/add
};

// In UpdateSavedAddressAsync
address.UpdatedAt = DateTime.UtcNow;   // ← uncomment
```

---

### Fix 9 — Dashboard N+1 Queries (Issue #9)

**File:** `AdminService.cs` — `GetDashboardMetricsAsync`

```csharp
// ❌ Current — 7 separate COUNT queries
var pendingCount    = await _ctx.Shipments.CountAsync(s => s.Status == ShipmentStatus.Pending);
var assignedCount   = await _ctx.Shipments.CountAsync(s => s.Status == ShipmentStatus.Assigned);
// ... 5 more

// ✅ Fix — single query with grouping
var statusCounts = await _ctx.Shipments
    .GroupBy(s => s.Status)
    .Select(g => new { Status = g.Key, Count = g.Count() })
    .ToListAsync();

var pendingCount    = statusCounts.FirstOrDefault(x => x.Status == ShipmentStatus.Pending)?.Count    ?? 0;
var assignedCount   = statusCounts.FirstOrDefault(x => x.Status == ShipmentStatus.Assigned)?.Count   ?? 0;
var inTransitCount  = statusCounts.FirstOrDefault(x => x.Status == ShipmentStatus.InTransit)?.Count  ?? 0;
var arrivedCount    = statusCounts.FirstOrDefault(x => x.Status == ShipmentStatus.Arrived)?.Count    ?? 0;
var deliveredCount  = statusCounts.FirstOrDefault(x => x.Status == ShipmentStatus.Delivered)?.Count  ?? 0;
var cancelledCount  = statusCounts.FirstOrDefault(x => x.Status == ShipmentStatus.Cancelled)?.Count  ?? 0;
var failedCount     = statusCounts.FirstOrDefault(x => x.Status == ShipmentStatus.FailedDelivery)?.Count ?? 0;

// Today's deliveries — one extra query is fine
var today           = DateTime.UtcNow.Date;
var deliveredToday  = await _ctx.Shipments
    .CountAsync(s => s.Status == ShipmentStatus.Delivered
                  && s.DeliveredAt.HasValue
                  && s.DeliveredAt.Value.Date == today);

// Pending approvals
var pendingApprovals = await _ctx.DriverProfiles
    .CountAsync(dp => dp.AccountStatus == DriverAccountStatus.PendingApproval);
```

---


---

### Fix 12 — OtpRegenerated Never Broadcast (Issue #21)

**File:** `OtpService.cs` — `RegenerateOtpAsync`

Already covered in Fix 5 above — add this after the Push call:

```csharp
await _tracking.BroadcastOtpRegeneratedAsync(
    shipment.TrackingNumber,
    otpType.ToString(),
    expiresAt);
```

---

### Fix 13 — Pagination Size Not Clamped (Issue #20)

**File:** `ShipmentService.cs`, `AdminService.cs` — all paginated methods

```csharp
// ✅ Add at the start of every paginated method
size = Math.Clamp(size, 1, 50);   // enforce max page size of 50
page = Math.Max(page, 1);          // page cannot be less than 1
```

---



---

## Priority Order for Implementation

```


  Fix 5 — Delivery OTP recipient fix → OTP goes to wrong person



  Fix 1 — Active delivery guard
  Fix 6 — SavedAddress 1:Many EF mapping
  Fix 7 — UpdateAsync wrong entity
  Fix 9 — Dashboard single query
  Fix 13 — Pagination clamp

CLEANUP (before submission):
  Fix 8  — UpdatedAt on addresses

  Fix 12 — OtpRegenerated broadcast


```