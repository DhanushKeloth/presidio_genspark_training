# Bus Booking System — Implementation Document

> **Stack:** Angular 17+ · .NET 8 Web API · PostgreSQL 16 · SignalR · NgRx  
> **Version:** 1.0 · April 2026  
> **Status:** Implementation Ready

---

## Table of Contents

1. [System Architecture](#1-system-architecture)
2. [Project Structure](#2-project-structure)
3. [Authentication & Authorization](#3-authentication--authorization)
4. [User Module — Implementation](#4-user-module--implementation)
5. [Operator Module — Implementation](#5-operator-module--implementation)
6. [Admin Module — Implementation](#6-admin-module--implementation)
7. [Seat Locking System](#7-seat-locking-system)
8. [Real-Time Updates with SignalR](#8-real-time-updates-with-signalr)
9. [Payment Integration](#9-payment-integration)
10. [PDF Ticket Generation](#10-pdf-ticket-generation)
11. [Email Notification System](#11-email-notification-system)
12. [API Reference](#12-api-reference)
13. [Angular Frontend Architecture](#13-angular-frontend-architecture)
14. [Security Checklist](#14-security-checklist)
15. [Background Jobs](#15-background-jobs)
16. [Error Handling Strategy](#16-error-handling-strategy)
17. [MVP Build Order](#17-mvp-build-order)

---

## 1. System Architecture

### 1.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        CLIENT LAYER                             │
│                                                                 │
│   ┌─────────────────────────────────────────────────────────┐  │
│   │              Angular 17+ SPA                            │  │
│   │   NgRx Store │ Angular Material │ SignalR Client        │  │
│   │   Modules: Auth | User | Operator | Admin               │  │
│   └──────────────────────┬──────────────────────────────────┘  │
└─────────────────────────┼───────────────────────────────────────┘
                          │  HTTPS + WSS
┌─────────────────────────▼───────────────────────────────────────┐
│                       API LAYER                                 │
│                                                                 │
│   ┌─────────────────────────────────────────────────────────┐  │
│   │               .NET 8 Web API                            │  │
│   │                                                         │  │
│   │  ┌──────────┐  ┌──────────┐  ┌──────────┐             │  │
│   │  │   Auth   │  │ Booking  │  │  Buses   │             │  │
│   │  │Controller│  │Controller│  │Controller│  ...        │  │
│   │  └──────────┘  └──────────┘  └──────────┘             │  │
│   │                                                         │  │
│   │  ┌──────────────────────────────────────┐              │  │
│   │  │         SignalR Hub                  │              │  │
│   │  │    SeatAvailabilityHub               │              │  │
│   │  └──────────────────────────────────────┘              │  │
│   │                                                         │  │
│   │  ┌──────────────────────────────────────┐              │  │
│   │  │       Background Services            │              │  │
│   │  │  SeatLockExpiry | NotificationSender │              │  │
│   │  └──────────────────────────────────────┘              │  │
│   └──────────────────────┬──────────────────────────────────┘  │
└─────────────────────────┼───────────────────────────────────────┘
                          │  EF Core / ADO.NET
┌─────────────────────────▼───────────────────────────────────────┐
│                      DATA LAYER                                 │
│                                                                 │
│   ┌───────────────────┐    ┌───────────────┐                   │
│   │   PostgreSQL 16   │    │  File Storage  │                   │
│   │                   │    │  (PDF Tickets) │                   │
│   │  users            │    └───────────────┘                   │
│   │  operators        │                                         │
│   │  buses            │    ┌───────────────┐                   │
│   │  seats            │    │    SendGrid   │                   │
│   │  seat_locks       │    │  (Email Queue)│                   │
│   │  bookings         │    └───────────────┘                   │
│   │  payments         │                                         │
│   │  notifications    │                                         │
│   └───────────────────┘                                         │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 Request Lifecycle

```
Browser Request
      │
      ▼
HTTPS Termination (Nginx / Azure Front Door)
      │
      ▼
.NET 8 Web API Pipeline
  ├─ Exception Middleware        ← global error catching
  ├─ Rate Limit Middleware       ← brute-force protection
  ├─ CORS Middleware             ← origin validation
  ├─ Authentication Middleware   ← JWT validation
  ├─ Authorization Middleware    ← RBAC role check
  ├─ [Controller Action]         ← business logic
  ├─ EF Core → PostgreSQL        ← data access
  └─ Response Serialization      ← JSON output
```

### 1.3 Technology Decisions

| Concern | Choice | Reason |
|---|---|---|
| Frontend framework | Angular 17 (standalone components) | Strong typing, DI, built-in forms |
| State management | NgRx | Predictable state for seat grid and booking flow |
| Backend framework | .NET 8 Web API | Strong EF Core integration, SignalR built-in |
| ORM | Entity Framework Core 8 | Migrations, LINQ, transactions |
| Real-time | ASP.NET Core SignalR | Native .NET, WebSocket fallback |
| Database | PostgreSQL 16 | Row-level locking, JSONB, UUID support |
| Authentication | JWT Bearer Tokens | Stateless, role claims |
| Background jobs | .NET BackgroundService | No extra dependency for MVP |
| PDF | QuestPDF | Free, fluent API, no Chrome dependency |
| Email | SendGrid | Reliable delivery, free tier for dev |
| Payments (v1) | Dummy gateway | Unblocks MVP |
| Payments (v2) | Stripe API | Stripe India supports UPI |

---

## 2. Project Structure

### 2.1 .NET Web API Structure

```
BusBooking.API/
├── Controllers/
│   ├── AuthController.cs
│   ├── BusesController.cs
│   ├── BookingsController.cs
│   ├── SeatsController.cs
│   ├── RoutesController.cs
│   ├── OperatorsController.cs
│   └── AdminController.cs
│
├── Hubs/
│   └── SeatAvailabilityHub.cs        ← SignalR hub
│
├── Services/
│   ├── Interfaces/
│   │   ├── IAuthService.cs
│   │   ├── IBookingService.cs
│   │   ├── ISeatLockService.cs
│   │   ├── IPaymentService.cs
│   │   ├── ITicketService.cs
│   │   └── INotificationService.cs
│   ├── AuthService.cs
│   ├── BookingService.cs
│   ├── SeatLockService.cs
│   ├── PaymentService.cs
│   ├── TicketService.cs
│   └── NotificationService.cs
│
├── BackgroundServices/
│   ├── SeatLockExpiryService.cs      ← runs every 60s
│   └── NotificationSenderService.cs  ← email queue processor
│
├── Data/
│   ├── AppDbContext.cs
│   ├── Migrations/
│   └── Repositories/
│       ├── BookingRepository.cs
│       └── SeatRepository.cs
│
├── Models/
│   ├── Entities/                     ← EF Core entities
│   │   ├── User.cs
│   │   ├── Operator.cs
│   │   ├── Bus.cs
│   │   ├── Seat.cs
│   │   ├── Booking.cs
│   │   ├── BookingDetail.cs
│   │   ├── Payment.cs
│   │   ├── SeatLock.cs
│   │   └── Notification.cs
│   └── DTOs/                         ← request/response shapes
│       ├── Auth/
│       ├── Booking/
│       ├── Bus/
│       └── Admin/
│
├── Middleware/
│   ├── ExceptionMiddleware.cs
│   └── RateLimitMiddleware.cs
│
├── Helpers/
│   ├── JwtHelper.cs
│   └── PasswordHelper.cs
│
├── appsettings.json
├── appsettings.Development.json
└── Program.cs
```

### 2.2 Angular Project Structure

```
src/
├── app/
│   ├── core/
│   │   ├── guards/
│   │   │   ├── auth.guard.ts
│   │   │   ├── role.guard.ts
│   │   │   └── operator.guard.ts
│   │   ├── interceptors/
│   │   │   ├── jwt.interceptor.ts
│   │   │   └── error.interceptor.ts
│   │   ├── services/
│   │   │   ├── auth.service.ts
│   │   │   ├── signalr.service.ts
│   │   │   └── storage.service.ts
│   │   └── models/
│   │       ├── user.model.ts
│   │       ├── booking.model.ts
│   │       └── bus.model.ts
│   │
│   ├── store/                        ← NgRx
│   │   ├── auth/
│   │   │   ├── auth.actions.ts
│   │   │   ├── auth.reducer.ts
│   │   │   ├── auth.effects.ts
│   │   │   └── auth.selectors.ts
│   │   ├── booking/
│   │   │   ├── booking.actions.ts
│   │   │   ├── booking.reducer.ts
│   │   │   ├── booking.effects.ts
│   │   │   └── booking.selectors.ts
│   │   └── seat/
│   │       ├── seat.actions.ts
│   │       ├── seat.reducer.ts
│   │       └── seat.selectors.ts
│   │
│   ├── features/
│   │   ├── auth/
│   │   │   ├── login/
│   │   │   ├── register/
│   │   │   └── forgot-password/
│   │   ├── user/
│   │   │   ├── search/
│   │   │   ├── seat-selection/
│   │   │   ├── checkout/
│   │   │   ├── booking-history/
│   │   │   └── profile/
│   │   ├── operator/
│   │   │   ├── dashboard/
│   │   │   ├── bus-management/
│   │   │   └── booking-monitor/
│   │   └── admin/
│   │       ├── dashboard/
│   │       ├── operator-management/
│   │       └── route-management/
│   │
│   ├── shared/
│   │   ├── components/
│   │   │   ├── seat-grid/
│   │   │   ├── countdown-timer/
│   │   │   └── bus-card/
│   │   └── pipes/
│   │
│   └── app.routes.ts
│
├── environments/
│   ├── environment.ts
│   └── environment.prod.ts
└── styles.scss
```

---

## 3. Authentication & Authorization

### 3.1 JWT Configuration

```json
// appsettings.json
{
  "JwtSettings": {
    "SecretKey": "ENV_VAR_ONLY_NEVER_IN_REPO",
    "Issuer": "BusBookingAPI",
    "Audience": "BusBookingClient",
    "ExpiryHours": 24
  }
}
```

### 3.2 Token Structure

```
Header:  { "alg": "HS256", "typ": "JWT" }

Payload: {
  "sub":   "user-uuid",
  "email": "user@example.com",
  "role":  "User",            // User | Operator | Admin
  "iat":   1714000000,
  "exp":   1714086400
}
```

### 3.3 Program.cs — Auth Setup

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience            = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]!))
        };

        // Allow JWT from SignalR query string (WebSocket auth)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("UserOnly",     p => p.RequireRole("User"));
    options.AddPolicy("OperatorOnly", p => p.RequireRole("Operator"));
    options.AddPolicy("AdminOnly",    p => p.RequireRole("Admin"));
});
```

### 3.4 Angular JWT Interceptor

```typescript
// jwt.interceptor.ts
export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(StorageService).getToken();
  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` }
    });
  }
  return next(req);
};
```

### 3.5 Route Guards

```typescript
// auth.guard.ts
export const authGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router      = inject(Router);

  if (authService.isLoggedIn()) return true;

  router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
  return false;
};

// role.guard.ts
export const roleGuard: CanActivateFn = (route) => {
  const authService    = inject(AuthService);
  const requiredRole   = route.data['role'] as string;

  if (authService.hasRole(requiredRole)) return true;
  return inject(Router).createUrlTree(['/unauthorized']);
};
```

---

## 4. User Module — Implementation

### 4.1 FR-U01: User Registration

**Functional Requirement:** Users register with name, email, password. Email must be unique. Password must be hashed.

```csharp
// AuthController.cs
[HttpPost("register")]
[AllowAnonymous]
public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto)
{
    if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
        return Conflict(new { message = "Email already registered." });

    var user = new User
    {
        Id           = Guid.NewGuid(),
        FullName     = dto.FullName,
        Email        = dto.Email.ToLower(),
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12),
        Status       = "active",
        CreatedAt    = DateTime.UtcNow,
        UpdatedAt    = DateTime.UtcNow
    };

    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    return Created(string.Empty, new { message = "Registration successful." });
}
```

**Validation DTOs:**

```csharp
// RegisterRequestDto.cs
public record RegisterRequestDto(
    [Required][StringLength(100, MinimumLength = 2)]
    string FullName,

    [Required][EmailAddress]
    string Email,

    [Required][StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[\W]).+$",
        ErrorMessage = "Password needs uppercase, number, and special character.")]
    string Password
);
```

---

### 4.2 FR-U02: Login

**Functional Requirement:** Login via email + password. Returns JWT with role claim.

```csharp
// AuthController.cs
[HttpPost("login")]
[AllowAnonymous]
public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
{
    var user = await _context.Users
        .FirstOrDefaultAsync(u => u.Email == dto.Email.ToLower());

    if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        return Unauthorized(new { message = "Invalid email or password." });

    if (user.Status != "active")
        return Forbid();

    var token = _jwtHelper.GenerateToken(user.Id.ToString(), user.Email, "User");

    return Ok(new { token, user = new { user.Id, user.FullName, user.Email } });
}
```

---

### 4.3 FR-U03: Bus Search

**Functional Requirement:** Search by source, destination, date. Returns matching active buses with available seat counts.

```csharp
// BusesController.cs
[HttpGet("search")]
[AllowAnonymous]
public async Task<IActionResult> Search(
    [FromQuery] string source,
    [FromQuery] string destination,
    [FromQuery] DateOnly journeyDate,
    [FromQuery] string? busType = null)
{
    var query = _context.Buses
        .Include(b => b.Operator)
        .Include(b => b.Route)
        .Include(b => b.Seats)
        .Where(b =>
            b.Route.SourceCity == source &&
            b.Route.DestinationCity == destination &&
            b.Status == BusStatus.Active);

    if (busType is not null)
        query = query.Where(b => b.BusType == busType);

    var buses = await query.ToListAsync();

    // Count booked seats for this journey date
    var bookedSeatIds = await _context.BookingDetails
        .Include(bd => bd.Booking)
        .Where(bd =>
            bd.Booking.JourneyDate == journeyDate &&
            bd.Booking.Status == BookingStatus.Confirmed)
        .Select(bd => bd.SeatId)
        .ToListAsync();

    // Count locked seats for this journey date
    var lockedSeatIds = await _context.SeatLocks
        .Where(sl =>
            sl.JourneyDate == journeyDate &&
            sl.LockExpiry > DateTime.UtcNow)
        .Select(sl => sl.SeatId)
        .ToListAsync();

    var unavailable = bookedSeatIds.Union(lockedSeatIds).ToHashSet();

    var result = buses.Select(b => new BusSearchResultDto
    {
        Id              = b.Id,
        BusName         = b.RegistrationNumber,
        OperatorName    = b.Operator.CompanyName,
        BusType         = b.BusType,
        DepartureTime   = b.DepartureTime,
        ArrivalTime     = b.ArrivalTime,
        PricePerSeat    = b.PricePerSeat,
        TotalSeats      = b.TotalSeats,
        AvailableSeats  = b.Seats.Count(s => s.IsActive && !unavailable.Contains(s.Id)),
        Amenities       = b.Amenities
    });

    return Ok(result);
}
```

---

### 4.4 FR-U04: View Seat Layout

**Functional Requirement:** Return the seat layout with each seat's live availability status.

```csharp
// SeatsController.cs
[HttpGet("buses/{busId}/seats")]
[AllowAnonymous]
public async Task<IActionResult> GetSeatLayout(
    Guid busId, [FromQuery] DateOnly journeyDate)
{
    var bus = await _context.Buses
        .Include(b => b.Seats)
        .FirstOrDefaultAsync(b => b.Id == busId);

    if (bus is null) return NotFound();

    var bookedIds = await _context.BookingDetails
        .Include(bd => bd.Booking)
        .Where(bd =>
            bd.Booking.BusId == busId &&
            bd.Booking.JourneyDate == journeyDate &&
            bd.Booking.Status == BookingStatus.Confirmed)
        .Select(bd => bd.SeatId)
        .ToHashSetAsync();

    var lockedIds = await _context.SeatLocks
        .Where(sl =>
            sl.BusId == busId &&
            sl.JourneyDate == journeyDate &&
            sl.LockExpiry > DateTime.UtcNow)
        .Select(sl => sl.SeatId)
        .ToHashSetAsync();

    var seats = bus.Seats.Select(s => new SeatStatusDto
    {
        Id          = s.Id,
        SeatNumber  = s.SeatNumber,
        SeatType    = s.SeatType,
        RowPosition = s.RowPosition,
        ColPosition = s.ColPosition,
        Status      = bookedIds.Contains(s.Id) ? "booked"
                    : lockedIds.Contains(s.Id) ? "locked"
                    : "available"
    });

    return Ok(new { SeatLayout = bus.SeatLayout, Seats = seats });
}
```

---

### 4.5 FR-U05: Create Booking

**Functional Requirement:** After payment confirmation, create a booking record with per-seat passenger details.

```csharp
// BookingsController.cs
[HttpPost]
[Authorize(Policy = "UserOnly")]
public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto dto)
{
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    await using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        // Verify all seats are still locked by this user
        var locks = await _context.SeatLocks
            .Where(sl =>
                dto.SeatIds.Contains(sl.SeatId) &&
                sl.UserId == userId &&
                sl.JourneyDate == dto.JourneyDate &&
                sl.LockExpiry > DateTime.UtcNow)
            .ToListAsync();

        if (locks.Count != dto.SeatIds.Count)
            return Conflict(new { message = "One or more seats are no longer reserved. Please restart." });

        // Create booking
        var booking = new Booking
        {
            Id          = Guid.NewGuid(),
            UserId      = userId,
            BusId       = dto.BusId,
            JourneyDate = dto.JourneyDate,
            TotalAmount = dto.TotalAmount,
            Status      = BookingStatus.Confirmed,
            CreatedAt   = DateTime.UtcNow,
            UpdatedAt   = DateTime.UtcNow
        };
        _context.Bookings.Add(booking);

        // Add per-seat details
        foreach (var passenger in dto.Passengers)
        {
            _context.BookingDetails.Add(new BookingDetail
            {
                Id              = Guid.NewGuid(),
                BookingId       = booking.Id,
                SeatId          = passenger.SeatId,
                PassengerName   = passenger.Name,
                PassengerAge    = passenger.Age,
                PassengerGender = passenger.Gender,
                SeatPrice       = passenger.SeatPrice
            });
        }

        // Create payment record
        _context.Payments.Add(new Payment
        {
            Id            = Guid.NewGuid(),
            BookingId     = booking.Id,
            Amount        = dto.TotalAmount,
            PaymentMethod = dto.PaymentMethod,
            PaymentStatus = PaymentStatus.Success,   // dummy for MVP
            PaidAt        = DateTime.UtcNow,
            CreatedAt     = DateTime.UtcNow
        });

        // Release seat locks
        _context.SeatLocks.RemoveRange(locks);

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        // Queue confirmation email
        await _notificationService.QueueBookingConfirmationAsync(booking.Id);

        // Queue PDF generation
        await _ticketService.GenerateAsync(booking.Id);

        // Broadcast seats as booked via SignalR
        await _hub.Clients.Group($"bus-{dto.BusId}-{dto.JourneyDate}")
            .SendAsync("SeatsBooked", dto.SeatIds);

        return Created($"/api/v1/bookings/{booking.Id}", new { bookingId = booking.Id });
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

---

### 4.6 FR-U06: Cancel Booking

**Functional Requirement:** User cancels a confirmed upcoming booking. Refund is initiated.

```csharp
// BookingsController.cs
[HttpPost("{id}/cancel")]
[Authorize(Policy = "UserOnly")]
public async Task<IActionResult> CancelBooking(Guid id, [FromBody] CancelDto dto)
{
    var userId  = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var booking = await _context.Bookings
        .Include(b => b.Payment)
        .Include(b => b.BookingDetails)
        .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

    if (booking is null)             return NotFound();
    if (booking.Status != BookingStatus.Confirmed) return BadRequest(new { message = "Only confirmed bookings can be cancelled." });
    if (booking.JourneyDate <= DateOnly.FromDateTime(DateTime.UtcNow.AddHours(2)))
        return BadRequest(new { message = "Cancellations are not allowed within 2 hours of departure." });

    booking.Status              = BookingStatus.Cancelled;
    booking.CancellationReason  = dto.Reason;
    booking.CancelledAt         = DateTime.UtcNow;
    booking.UpdatedAt           = DateTime.UtcNow;

    if (booking.Payment is not null)
        booking.Payment.PaymentStatus = PaymentStatus.Refunded; // dummy

    await _context.SaveChangesAsync();
    await _notificationService.QueueCancellationEmailAsync(booking.Id);

    return Ok(new { message = "Booking cancelled successfully." });
}
```

---

## 5. Operator Module — Implementation

### 5.1 FR-O01: Operator Registration

```csharp
// AuthController.cs — operator registration
[HttpPost("operators/register")]
[AllowAnonymous]
public async Task<IActionResult> RegisterOperator([FromBody] OperatorRegisterDto dto)
{
    if (await _context.Operators.AnyAsync(o => o.Email == dto.Email))
        return Conflict(new { message = "Email already registered." });

    var op = new Operator
    {
        Id           = Guid.NewGuid(),
        CompanyName  = dto.CompanyName,
        Email        = dto.Email.ToLower(),
        Phone        = dto.Phone,
        GstNumber    = dto.GstNumber,
        Address      = dto.Address,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12),
        Status       = OperatorStatus.Pending,
        CreatedAt    = DateTime.UtcNow,
        UpdatedAt    = DateTime.UtcNow
    };

    _context.Operators.Add(op);
    await _context.SaveChangesAsync();

    // Notify admin
    await _notificationService.QueueAdminNewOperatorAlertAsync(op.Id);

    return Created(string.Empty, new { message = "Registration submitted. Awaiting admin approval." });
}
```

---

### 5.2 FR-O02: Add Bus

```csharp
// BusesController.cs
[HttpPost]
[Authorize(Policy = "OperatorOnly")]
public async Task<IActionResult> AddBus([FromBody] AddBusDto dto)
{
    var operatorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    var op = await _context.Operators
        .FirstOrDefaultAsync(o => o.Id == operatorId);

    if (op?.Status != OperatorStatus.Approved)
        return Forbid();

    if (await _context.Buses.AnyAsync(b => b.RegistrationNumber == dto.RegistrationNumber))
        return Conflict(new { message = "Registration number already exists." });

    var bus = new Bus
    {
        Id                 = Guid.NewGuid(),
        OperatorId         = operatorId,
        RouteId            = dto.RouteId,
        RegistrationNumber = dto.RegistrationNumber,
        BusType            = dto.BusType,
        TotalSeats         = dto.TotalSeats,
        DepartureTime      = dto.DepartureTime,
        ArrivalTime        = dto.ArrivalTime,
        BoardingLocation   = dto.BoardingLocation,
        DroppingLocation   = dto.DroppingLocation,
        PricePerSeat       = dto.PricePerSeat,
        Amenities          = dto.Amenities,
        SeatLayout         = dto.SeatLayout,
        Status             = BusStatus.Active,
        CreatedAt          = DateTime.UtcNow,
        UpdatedAt          = DateTime.UtcNow
    };

    _context.Buses.Add(bus);

    // Auto-generate seat records from layout
    for (int row = 1; row <= dto.Rows; row++)
    {
        for (int col = 1; col <= dto.Cols; col++)
        {
            var seatDef = dto.SeatLayout.FirstOrDefault(s => s.Row == row && s.Col == col);
            if (seatDef is null) continue;

            _context.Seats.Add(new Seat
            {
                Id          = Guid.NewGuid(),
                BusId       = bus.Id,
                SeatNumber  = seatDef.SeatNumber,
                SeatType    = seatDef.Type,
                RowPosition = row,
                ColPosition = col,
                IsActive    = true
            });
        }
    }

    await _context.SaveChangesAsync();
    return Created($"/api/v1/buses/{bus.Id}", new { busId = bus.Id });
}
```

---

### 5.3 FR-O03: Update Bus Status

```csharp
// BusesController.cs
[HttpPatch("{id}/status")]
[Authorize(Policy = "OperatorOnly")]
public async Task<IActionResult> UpdateBusStatus(Guid id, [FromBody] UpdateBusStatusDto dto)
{
    var operatorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var bus = await _context.Buses
        .FirstOrDefaultAsync(b => b.Id == id && b.OperatorId == operatorId);

    if (bus is null) return NotFound();

    bus.Status    = dto.Status;
    bus.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();

    // If permanently removed, cancel affected bookings and notify users
    if (dto.Status == BusStatus.Removed)
        await _bookingService.CancelFutureBookingsForBusAsync(bus.Id);

    return Ok(new { message = "Bus status updated." });
}
```

---

## 6. Admin Module — Implementation

### 6.1 FR-A01: Approve / Reject Operator

```csharp
// AdminController.cs
[HttpPatch("operators/{id}")]
[Authorize(Policy = "AdminOnly")]
public async Task<IActionResult> UpdateOperatorStatus(
    Guid id, [FromBody] OperatorStatusUpdateDto dto)
{
    var adminId  = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var op = await _context.Operators.FindAsync(id);

    if (op is null) return NotFound();

    op.Status          = dto.Status;
    op.ApprovedBy      = adminId;
    op.ApprovedAt      = DateTime.UtcNow;
    op.RejectionReason = dto.Reason;
    op.UpdatedAt       = DateTime.UtcNow;

    await _context.SaveChangesAsync();
    await _notificationService.QueueOperatorStatusChangeEmailAsync(op.Id, dto.Status, dto.Reason);

    return Ok(new { message = $"Operator {dto.Status} successfully." });
}
```

---

### 6.2 FR-A02: Create Route

```csharp
// AdminController.cs
[HttpPost("routes")]
[Authorize(Policy = "AdminOnly")]
public async Task<IActionResult> CreateRoute([FromBody] CreateRouteDto dto)
{
    var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    if (await _context.Routes.AnyAsync(r =>
        r.SourceCity == dto.SourceCity &&
        r.DestinationCity == dto.DestinationCity))
        return Conflict(new { message = "Route already exists." });

    var route = new Route
    {
        Id              = Guid.NewGuid(),
        SourceCity      = dto.SourceCity,
        DestinationCity = dto.DestinationCity,
        DistanceKm      = dto.DistanceKm,
        EstimatedHours  = dto.EstimatedHours,
        IsActive        = true,
        CreatedBy       = adminId,
        CreatedAt       = DateTime.UtcNow,
        UpdatedAt       = DateTime.UtcNow
    };

    _context.Routes.Add(route);
    await _context.SaveChangesAsync();

    return Created($"/api/v1/routes/{route.Id}", route);
}
```

---

### 6.3 FR-A03: Revenue Dashboard

```csharp
// AdminController.cs
[HttpGet("dashboard")]
[Authorize(Policy = "AdminOnly")]
public async Task<IActionResult> GetDashboard(
    [FromQuery] DateTime from, [FromQuery] DateTime to)
{
    var bookings = await _context.Bookings
        .Include(b => b.Payment)
        .Include(b => b.Bus).ThenInclude(bus => bus.Operator)
        .Where(b =>
            b.Status == BookingStatus.Confirmed &&
            b.CreatedAt >= from && b.CreatedAt <= to)
        .ToListAsync();

    return Ok(new
    {
        TotalBookings      = bookings.Count,
        TotalRevenue       = bookings.Sum(b => b.TotalAmount),
        RevenueByOperator  = bookings
            .GroupBy(b => b.Bus.Operator.CompanyName)
            .Select(g => new { Operator = g.Key, Revenue = g.Sum(b => b.TotalAmount) }),
        CancellationCount  = await _context.Bookings
            .CountAsync(b => b.Status == BookingStatus.Cancelled &&
                             b.CancelledAt >= from && b.CancelledAt <= to),
    });
}
```

---

## 7. Seat Locking System

### 7.1 FR-LOCK-01: Acquire Seat Lock

**This is the most critical operation. Must be atomic.**

```csharp
// SeatLockService.cs
public async Task<SeatLockResult> AcquireLocksAsync(
    List<Guid> seatIds, Guid userId, Guid busId, DateOnly journeyDate)
{
    await using var tx = await _context.Database.BeginTransactionAsync(
        IsolationLevel.ReadCommitted);

    try
    {
        // Raw SQL: SELECT FOR UPDATE SKIP LOCKED
        // Atomically checks for existing non-expired locks
        var existingLocks = await _context.SeatLocks
            .FromSqlRaw(@"
                SELECT * FROM seat_locks
                WHERE  seat_id = ANY({0}::uuid[])
                  AND  journey_date = {1}
                  AND  lock_expiry > NOW()
                FOR UPDATE SKIP LOCKED",
                seatIds.Select(id => id.ToString()).ToArray(),
                journeyDate)
            .ToListAsync();

        if (existingLocks.Any())
        {
            await tx.RollbackAsync();
            return SeatLockResult.Conflict(existingLocks.Select(l => l.SeatId).ToList());
        }

        var expiry = DateTime.UtcNow.AddMinutes(10);
        var locks  = seatIds.Select(seatId => new SeatLock
        {
            Id          = Guid.NewGuid(),
            SeatId      = seatId,
            UserId      = userId,
            BusId       = busId,
            JourneyDate = journeyDate,
            LockExpiry  = expiry,
            CreatedAt   = DateTime.UtcNow
        }).ToList();

        _context.SeatLocks.AddRange(locks);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        // Broadcast lock event to all clients on this bus/date
        await _hub.Clients
            .Group($"bus-{busId}-{journeyDate}")
            .SendAsync("SeatsLocked", seatIds);

        return SeatLockResult.Success(expiry);
    }
    catch
    {
        await tx.RollbackAsync();
        throw;
    }
}
```

### 7.2 FR-LOCK-02: Lock Expiry Endpoint

```csharp
// SeatsController.cs
[HttpPost("lock")]
[Authorize(Policy = "UserOnly")]
public async Task<IActionResult> LockSeats([FromBody] LockSeatsDto dto)
{
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var result = await _seatLockService.AcquireLocksAsync(
        dto.SeatIds, userId, dto.BusId, dto.JourneyDate);

    if (!result.Success)
        return Conflict(new
        {
            message       = "Some seats were just taken. Please reselect.",
            conflictSeats = result.ConflictSeatIds
        });

    return Ok(new
    {
        lockExpiry     = result.LockExpiry,
        remainingMs    = (result.LockExpiry - DateTime.UtcNow).TotalMilliseconds
    });
}

[HttpDelete("lock")]
[Authorize(Policy = "UserOnly")]
public async Task<IActionResult> ReleaseLocks([FromBody] ReleaseLockDto dto)
{
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var locks  = await _context.SeatLocks
        .Where(sl => dto.SeatIds.Contains(sl.SeatId) && sl.UserId == userId)
        .ToListAsync();

    _context.SeatLocks.RemoveRange(locks);
    await _context.SaveChangesAsync();

    await _hub.Clients
        .Group($"bus-{dto.BusId}-{dto.JourneyDate}")
        .SendAsync("SeatsReleased", dto.SeatIds);

    return NoContent();
}
```

---

## 8. Real-Time Updates with SignalR

### 8.1 Server — SeatAvailabilityHub

```csharp
// Hubs/SeatAvailabilityHub.cs
[Authorize]
public class SeatAvailabilityHub : Hub
{
    // Client calls this when they open the seat selection screen
    public async Task JoinBusGroup(string busId, string journeyDate)
    {
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"bus-{busId}-{journeyDate}");
    }

    public async Task LeaveBusGroup(string busId, string journeyDate)
    {
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            $"bus-{busId}-{journeyDate}");
    }
}
```

**Register in Program.cs:**

```csharp
builder.Services.AddSignalR();
// ...
app.MapHub<SeatAvailabilityHub>("/hubs/seat-availability");
```

---

### 8.2 Angular — SignalR Service

```typescript
// signalr.service.ts
@Injectable({ providedIn: 'root' })
export class SignalRService {
  private connection: signalR.HubConnection | null = null;

  readonly seatUpdates$ = new Subject<{ type: string; seatIds: string[] }>();

  connect(token: string): void {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/seat-availability', { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .build();

    this.connection.on('SeatsLocked',   ids => this.seatUpdates$.next({ type: 'locked',    seatIds: ids }));
    this.connection.on('SeatsBooked',   ids => this.seatUpdates$.next({ type: 'booked',    seatIds: ids }));
    this.connection.on('SeatsReleased', ids => this.seatUpdates$.next({ type: 'available', seatIds: ids }));

    this.connection.start().catch(console.error);
  }

  joinBusGroup(busId: string, journeyDate: string): void {
    this.connection?.invoke('JoinBusGroup', busId, journeyDate);
  }

  disconnect(): void {
    this.connection?.stop();
  }
}
```

---

### 8.3 NgRx — Seat State

```typescript
// seat.reducer.ts
export interface SeatState {
  seats: SeatStatusDto[];
  selectedSeatIds: string[];
  lockExpiry: Date | null;
}

export const seatReducer = createReducer(
  initialState,
  on(SeatActions.loadSeatsSuccess, (state, { seats }) =>
    ({ ...state, seats })),

  on(SeatActions.seatLocked, (state, { seatIds }) => ({
    ...state,
    seats: state.seats.map(s =>
      seatIds.includes(s.id) ? { ...s, status: 'locked' } : s)
  })),

  on(SeatActions.seatBooked, (state, { seatIds }) => ({
    ...state,
    seats: state.seats.map(s =>
      seatIds.includes(s.id) ? { ...s, status: 'booked' } : s)
  })),

  on(SeatActions.seatReleased, (state, { seatIds }) => ({
    ...state,
    seats: state.seats.map(s =>
      seatIds.includes(s.id) ? { ...s, status: 'available' } : s)
  })),

  on(SeatActions.toggleSeatSelection, (state, { seatId }) => {
    const already = state.selectedSeatIds.includes(seatId);
    return {
      ...state,
      selectedSeatIds: already
        ? state.selectedSeatIds.filter(id => id !== seatId)
        : [...state.selectedSeatIds, seatId]
    };
  }),
);
```

---

## 9. Payment Integration

### 9.1 Phase 1 — Dummy Payment

```csharp
// Services/PaymentService.cs
public class DummyPaymentService : IPaymentService
{
    public Task<PaymentResult> ProcessAsync(PaymentRequestDto dto)
    {
        // Simulate network delay
        return Task.FromResult(new PaymentResult
        {
            Success   = true,
            PaymentId = $"DUMMY-{Guid.NewGuid():N}".ToUpper(),
            Message   = "Payment simulated successfully."
        });
    }
}
```

### 9.2 Phase 2 — Stripe Integration

```csharp
// Services/StripePaymentService.cs
public class StripePaymentService : IPaymentService
{
    private readonly PaymentIntentService _intentService;

    public StripePaymentService(IConfiguration config)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
        _intentService = new PaymentIntentService();
    }

    public async Task<PaymentResult> CreateIntentAsync(decimal amount)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount   = (long)(amount * 100),   // paise
            Currency = "inr",
            AutomaticPaymentMethods = new() { Enabled = true }
        };

        var intent = await _intentService.CreateAsync(options);
        return new PaymentResult { ClientSecret = intent.ClientSecret };
    }

    public async Task<PaymentResult> ConfirmAsync(string paymentIntentId)
    {
        var intent = await _intentService.GetAsync(paymentIntentId);
        return new PaymentResult
        {
            Success   = intent.Status == "succeeded",
            PaymentId = intent.Id
        };
    }
}
```

**Angular — Stripe Elements:**

```typescript
// checkout.component.ts
ngOnInit(): void {
  this.stripe  = Stripe(environment.stripePublicKey);
  this.elements = this.stripe.elements();
  this.card     = this.elements.create('card');
  this.card.mount('#card-element');
}

async pay(): Promise<void> {
  const { clientSecret } = await this.paymentService.createIntent(this.total).toPromise();

  const { error, paymentIntent } = await this.stripe.confirmCardPayment(clientSecret, {
    payment_method: { card: this.card }
  });

  if (error)         this.error = error.message!;
  else if (paymentIntent?.status === 'succeeded')
    this.bookingService.createBooking({ ...this.bookingData, paymentIntentId: paymentIntent.id });
}
```

---

## 10. PDF Ticket Generation

### 10.1 QuestPDF Implementation

```csharp
// Services/TicketService.cs
public async Task<string> GenerateAsync(Guid bookingId)
{
    var booking = await _context.Bookings
        .Include(b => b.BookingDetails)
        .Include(b => b.Bus).ThenInclude(bus => bus.Route)
        .Include(b => b.Bus).ThenInclude(bus => bus.Operator)
        .Include(b => b.User)
        .FirstOrDefaultAsync(b => b.Id == bookingId)
        ?? throw new Exception("Booking not found");

    var filename = $"ticket-{bookingId}.pdf";
    var path     = Path.Combine(_config["TicketStorage:Path"]!, filename);

    var document = Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);

            page.Header().Row(row =>
            {
                row.RelativeItem().Text("BUS TICKET").Bold().FontSize(24);
                row.ConstantItem(100).Text(booking.Id.ToString()[..8].ToUpper())
                   .FontSize(12).AlignRight();
            });

            page.Content().Column(col =>
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });

                    table.Cell().Text("Operator");
                    table.Cell().Text(booking.Bus.Operator.CompanyName);

                    table.Cell().Text("Route");
                    table.Cell().Text($"{booking.Bus.Route.SourceCity} → {booking.Bus.Route.DestinationCity}");

                    table.Cell().Text("Journey Date");
                    table.Cell().Text(booking.JourneyDate.ToString("dd MMM yyyy"));

                    table.Cell().Text("Departure");
                    table.Cell().Text(booking.Bus.DepartureTime.ToString("hh:mm tt"));
                });

                col.Item().PaddingTop(20).Text("Passengers").Bold();

                foreach (var detail in booking.BookingDetails)
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Seat {detail.Seat.SeatNumber}");
                        row.RelativeItem().Text(detail.PassengerName);
                        row.ConstantItem(60).Text($"Age: {detail.PassengerAge}");
                        row.ConstantItem(80).Text($"₹ {detail.SeatPrice:N2}").AlignRight();
                    });
                }

                col.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem();
                    row.ConstantItem(160).Text($"TOTAL: ₹ {booking.TotalAmount:N2}")
                       .Bold().FontSize(14).AlignRight();
                });
            });
        });
    });

    document.GeneratePdf(path);

    booking.TicketPdfPath = filename;
    await _context.SaveChangesAsync();

    return path;
}
```

---

## 11. Email Notification System

### 11.1 Notification Queue Pattern

```csharp
// Services/NotificationService.cs
public async Task QueueBookingConfirmationAsync(Guid bookingId)
{
    var booking = await _context.Bookings
        .Include(b => b.User)
        .Include(b => b.Bus).ThenInclude(bus => bus.Route)
        .FirstAsync(b => b.Id == bookingId);

    _context.Notifications.Add(new Notification
    {
        RecipientId   = booking.UserId,
        RecipientType = "user",
        Type          = "booking_confirmed",
        Subject       = $"Your booking is confirmed — {booking.Id.ToString()[..8].ToUpper()}",
        Body          = BuildBookingConfirmationHtml(booking),
        Status        = "pending",
        CreatedAt     = DateTime.UtcNow
    });

    await _context.SaveChangesAsync();
}
```

### 11.2 Background Email Sender

```csharp
// BackgroundServices/NotificationSenderService.cs
public class NotificationSenderService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await ProcessPendingNotificationsAsync();
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }

    private async Task ProcessPendingNotificationsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sendGrid = scope.ServiceProvider.GetRequiredService<ISendGridClient>();

        var pending = await ctx.Notifications
            .Where(n => n.Status == "pending")
            .Take(50)
            .ToListAsync();

        foreach (var notification in pending)
        {
            try
            {
                var recipient = await GetEmailAsync(ctx, notification);
                var msg = MailHelper.CreateSingleEmail(
                    from: new EmailAddress("noreply@busbook.in"),
                    to:   new EmailAddress(recipient),
                    subject: notification.Subject,
                    plainTextContent: null,
                    htmlContent: notification.Body);

                await sendGrid.SendEmailAsync(msg);

                notification.Status = "sent";
                notification.SentAt = DateTime.UtcNow;
            }
            catch
            {
                notification.Status = "failed";
            }
        }

        await ctx.SaveChangesAsync();
    }
}
```

---

## 12. API Reference

### 12.1 Authentication

| Method | Endpoint | Auth | Request Body | Response |
|---|---|---|---|---|
| `POST` | `/api/v1/auth/register` | None | `{ fullName, email, password }` | `201 Created` |
| `POST` | `/api/v1/auth/login` | None | `{ email, password }` | `200 { token, user }` |
| `POST` | `/api/v1/auth/operators/register` | None | `{ companyName, email, phone, password }` | `201 Created` |
| `POST` | `/api/v1/auth/forgot-password` | None | `{ email }` | `200 OK` |
| `POST` | `/api/v1/auth/reset-password` | None | `{ token, newPassword }` | `200 OK` |

### 12.2 Buses

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/v1/buses/search?source=&destination=&date=` | None | Search available buses |
| `GET` | `/api/v1/buses/:id` | None | Get bus details |
| `POST` | `/api/v1/buses` | Operator | Add new bus |
| `PUT` | `/api/v1/buses/:id` | Operator | Update bus details |
| `PATCH` | `/api/v1/buses/:id/status` | Operator/Admin | Change bus status |

### 12.3 Seats

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/v1/buses/:id/seats?journeyDate=` | None | Get seat layout with live status |
| `POST` | `/api/v1/seats/lock` | User | Acquire seat locks |
| `DELETE` | `/api/v1/seats/lock` | User | Release seat locks manually |

### 12.4 Bookings

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/v1/bookings` | User | Create booking after payment |
| `GET` | `/api/v1/bookings` | User | List user's bookings |
| `GET` | `/api/v1/bookings/:id` | User | Get single booking detail |
| `POST` | `/api/v1/bookings/:id/cancel` | User | Cancel booking |
| `GET` | `/api/v1/bookings/:id/ticket` | User | Download PDF ticket |

### 12.5 Admin

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/v1/admin/operators` | Admin | List all operators |
| `PATCH` | `/api/v1/admin/operators/:id` | Admin | Approve/reject/disable |
| `POST` | `/api/v1/admin/routes` | Admin | Create route |
| `GET` | `/api/v1/admin/routes` | Admin | List all routes |
| `PATCH` | `/api/v1/admin/routes/:id` | Admin | Update/deactivate route |
| `GET` | `/api/v1/admin/dashboard` | Admin | Revenue analytics |

### 12.6 Standard Error Responses

```json
// 400 Bad Request
{ "message": "Validation failed.", "errors": { "email": ["Invalid email format"] } }

// 401 Unauthorized
{ "message": "Authentication required." }

// 403 Forbidden
{ "message": "You do not have permission to perform this action." }

// 404 Not Found
{ "message": "Resource not found." }

// 409 Conflict
{ "message": "One or more seats are already locked.", "conflictSeats": ["uuid1", "uuid2"] }

// 500 Internal Server Error
{ "message": "An unexpected error occurred.", "traceId": "abc123" }
```

---

## 13. Angular Frontend Architecture

### 13.1 Routing

```typescript
// app.routes.ts
export const routes: Routes = [
  { path: '',             redirectTo: '/search', pathMatch: 'full' },
  { path: 'login',        loadComponent: () => import('./features/auth/login/login.component') },
  { path: 'register',     loadComponent: () => import('./features/auth/register/register.component') },

  // User routes (auth required)
  { path: 'search',       loadComponent: () => import('./features/user/search/search.component') },
  { path: 'buses/:id/seats', loadComponent: () => import('./features/user/seat-selection/seat-selection.component'),
    canActivate: [authGuard] },
  { path: 'checkout',     loadComponent: () => import('./features/user/checkout/checkout.component'),
    canActivate: [authGuard] },
  { path: 'bookings',     loadComponent: () => import('./features/user/booking-history/booking-history.component'),
    canActivate: [authGuard] },

  // Operator routes
  { path: 'operator',     loadChildren: () => import('./features/operator/operator.routes'),
    canActivate: [authGuard, roleGuard], data: { role: 'Operator' } },

  // Admin routes
  { path: 'admin',        loadChildren: () => import('./features/admin/admin.routes'),
    canActivate: [authGuard, roleGuard], data: { role: 'Admin' } },

  { path: '**',           redirectTo: '/search' }
];
```

### 13.2 Seat Grid Component

```typescript
// seat-grid.component.ts
@Component({
  selector: 'app-seat-grid',
  template: `
    <div class="seat-grid" [style.grid-template-columns]="gridColumns">
      @for (seat of seats(); track seat.id) {
        <button
          class="seat"
          [class.available]="seat.status === 'available'"
          [class.selected]="isSelected(seat.id)"
          [class.locked]="seat.status === 'locked'"
          [class.booked]="seat.status === 'booked'"
          [disabled]="seat.status !== 'available' && !isSelected(seat.id)"
          (click)="toggleSeat(seat)">
          {{ seat.seatNumber }}
        </button>
      }
    </div>
  `
})
export class SeatGridComponent {
  seats       = input.required<SeatStatusDto[]>();
  selectedIds = input<string[]>([]);
  seatToggled = output<SeatStatusDto>();

  isSelected = (id: string) => this.selectedIds().includes(id);

  toggleSeat(seat: SeatStatusDto): void {
    if (seat.status === 'available' || this.isSelected(seat.id))
      this.seatToggled.emit(seat);
  }

  get gridColumns(): string {
    const cols = Math.max(...this.seats().map(s => s.colPosition));
    return `repeat(${cols}, 44px)`;
  }
}
```

### 13.3 Countdown Timer Component

```typescript
// countdown-timer.component.ts
@Component({
  selector: 'app-countdown',
  template: `
    <div class="timer" [class.warning]="remainingMs() < 120000">
      ⏱ {{ formattedTime() }}
    </div>
  `
})
export class CountdownTimerComponent implements OnInit, OnDestroy {
  expiryDate  = input.required<Date>();
  expired     = output<void>();

  remainingMs = signal(0);
  private interval?: ReturnType<typeof setInterval>;

  ngOnInit(): void {
    this.tick();
    this.interval = setInterval(() => this.tick(), 1000);
  }

  private tick(): void {
    const ms = new Date(this.expiryDate()).getTime() - Date.now();
    if (ms <= 0) {
      this.remainingMs.set(0);
      clearInterval(this.interval);
      this.expired.emit();
    } else {
      this.remainingMs.set(ms);
    }
  }

  formattedTime = computed(() => {
    const total   = Math.floor(this.remainingMs() / 1000);
    const minutes = Math.floor(total / 60).toString().padStart(2, '0');
    const seconds = (total % 60).toString().padStart(2, '0');
    return `${minutes}:${seconds}`;
  });

  ngOnDestroy(): void { clearInterval(this.interval); }
}
```

---

## 14. Security Checklist

| # | Requirement | Implementation | Status |
|---|---|---|---|
| S01 | Passwords hashed | `BCrypt.HashPassword(password, workFactor: 12)` | ✅ Required |
| S02 | JWT signed with secret | `HS256, secret from env var` | ✅ Required |
| S03 | RBAC on every endpoint | `[Authorize(Policy = "UserOnly")]` | ✅ Required |
| S04 | HTTPS enforced | `app.UseHttpsRedirection()` | ✅ Required |
| S05 | SQL injection prevented | EF Core parameterized queries | ✅ Required |
| S06 | CORS restricted | Named policy with allowed origins | ✅ Required |
| S07 | Rate limit on auth | 5 attempts / 15 min lockout | ✅ Required |
| S08 | Secrets in env vars | `Environment.GetEnvironmentVariable()` | ✅ Required |
| S09 | User owns resource | Validate `userId == booking.UserId` | ✅ Required |
| S10 | Operator owns bus | Validate `operatorId == bus.OperatorId` | ✅ Required |
| S11 | Reset token single-use | Null token after use | ✅ Required |
| S12 | Stripe webhook validation | Validate `Stripe-Signature` header | ⏳ Phase 2 |

---

## 15. Background Jobs

### 15.1 Seat Lock Expiry Service

```csharp
// BackgroundServices/SeatLockExpiryService.cs
public class SeatLockExpiryService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await ExpireLocksAsync();
            await Task.Delay(TimeSpan.FromSeconds(60), ct);
        }
    }

    private async Task ExpireLocksAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hub  = scope.ServiceProvider.GetRequiredService<IHubContext<SeatAvailabilityHub>>();

        var expired = await ctx.SeatLocks
            .Where(sl => sl.LockExpiry < DateTime.UtcNow)
            .ToListAsync();

        if (!expired.Any()) return;

        // Group by bus + date for targeted broadcasts
        var groups = expired.GroupBy(sl => (sl.BusId, sl.JourneyDate));

        ctx.SeatLocks.RemoveRange(expired);
        await ctx.SaveChangesAsync();

        foreach (var group in groups)
        {
            await hub.Clients
                .Group($"bus-{group.Key.BusId}-{group.Key.JourneyDate}")
                .SendAsync("SeatsReleased", group.Select(sl => sl.SeatId).ToList());
        }
    }
}
```

### 15.2 Register Background Services

```csharp
// Program.cs
builder.Services.AddHostedService<SeatLockExpiryService>();
builder.Services.AddHostedService<NotificationSenderService>();
```

---

## 16. Error Handling Strategy

### 16.1 Global Exception Middleware

```csharp
// Middleware/ExceptionMiddleware.cs
public class ExceptionMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context, new { message = "Validation failed.", errors = ex.Errors });
        }
        catch (NotFoundException ex)
        {
            context.Response.StatusCode = 404;
            await WriteJsonAsync(context, new { message = ex.Message });
        }
        catch (ConflictException ex)
        {
            context.Response.StatusCode = 409;
            await WriteJsonAsync(context, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = 500;
            await WriteJsonAsync(context, new
            {
                message = "An unexpected error occurred.",
                traceId = Activity.Current?.Id ?? context.TraceIdentifier
            });
        }
    }
}
```

### 16.2 Angular Error Interceptor

```typescript
// error.interceptor.ts
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const snack  = inject(MatSnackBar);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      switch (error.status) {
        case 401:
          router.navigate(['/login']);
          break;
        case 403:
          snack.open('You do not have permission for this action.', 'Close', { duration: 4000 });
          break;
        case 409:
          snack.open(error.error?.message ?? 'Conflict error.', 'Close', { duration: 4000 });
          break;
        case 500:
          snack.open('A server error occurred. Please try again.', 'Close', { duration: 5000 });
          break;
      }
      return throwError(() => error);
    })
  );
};
```

---

## 17. MVP Build Order

Build in this sequence to always have a runnable, testable application at each step.

```
Phase 1 — Foundation (Day 1 Morning)
  ✅ Step 1  PostgreSQL schema + EF Core migrations
  ✅ Step 2  .NET project setup — middleware, JWT, CORS
  ✅ Step 3  Admin seeds (admin user, 2-3 routes, 1 test operator)

Phase 2 — Core API (Day 1 Afternoon)
  ✅ Step 4  User registration + login
  ✅ Step 5  Operator registration + admin approval
  ✅ Step 6  Add bus + auto-generate seats
  ✅ Step 7  Bus search API
  ✅ Step 8  Seat layout API (with live status)

Phase 3 — Booking Flow (Day 1 Evening)
  ✅ Step 9  Seat lock acquire + release endpoints
  ✅ Step 10 Seat lock expiry background service
  ✅ Step 11 Create booking (dummy payment)
  ✅ Step 12 Cancel booking
  ✅ Step 13 Booking history API

Phase 4 — Angular Frontend (Day 2 Morning)
  ✅ Step 14 Auth pages (login, register)
  ✅ Step 15 Bus search + results page
  ✅ Step 16 Seat selection grid component
  ✅ Step 17 Checkout + booking confirmation

Phase 5 — Real-Time + Polish (Day 2 Afternoon)
  ✅ Step 18 SignalR hub + Angular client integration
  ✅ Step 19 Countdown timer component
  ✅ Step 20 PDF ticket generation
  ✅ Step 21 Operator dashboard (add bus, view bookings)
  ✅ Step 22 Admin panel (approve operators, manage routes)

Phase 6 — Post-MVP
  ⏳ Step 23 Stripe payment integration
  ⏳ Step 24 SendGrid email notifications
  ⏳ Step 25 QR codes on tickets
  ⏳ Step 26 Revenue analytics dashboard
```

---

> **Key Reminder:** The seat locking mechanism (Steps 9–10) is the technical centrepiece of this system. If concurrency is handled correctly at the database level using `SELECT FOR UPDATE SKIP LOCKED`, the rest of the booking flow is straightforward. Build and test seat locking in isolation before wiring it into the checkout flow.

---

*Bus Booking System — Implementation Document v1.0 · Angular + .NET 8 + PostgreSQL*
