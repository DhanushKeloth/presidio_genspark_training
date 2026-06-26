# OOP Concepts in BusBookingAPI — A Learning Document

> **Project:** Bus Booking System — .NET 8 REST API  
> **Tech Stack:** C# · ASP.NET Core · Entity Framework Core · PostgreSQL · JWT Auth  
> **Author:** Dhanush Keloth  
> **Date:** May 2026

---

## Table of Contents

1. [Classes & Models](#1-classes--models)
2. [Getters & Setters (Properties)](#2-getters--setters-properties)
3. [Interfaces](#3-interfaces)
4. [Encapsulation](#4-encapsulation)
5. [Inheritance](#5-inheritance)
6. [Abstraction](#6-abstraction)
7. [Polymorphism](#7-polymorphism)
8. [Summary Table](#8-summary-table)

---

## 1. Classes & Models

### ✅ Already Done

A **class** is the blueprint of a real-world object — it defines what data it holds and what it can do.

In our Bus Booking API, every real-world concept is modelled as its own C# class. The `User` class represents a passenger, the `Bus` class represents a vehicle, `Booking` represents a confirmed ticket, and so on — 11 entity classes in total, each in `Models/Entities/`. Each entity class maps directly to a PostgreSQL table using EF Core annotations such as `[Table("users")]`, which means the class acts as both the in-memory object and the schema definition for the database. On top of entities, we have a second family of pure data-shape classes called DTOs (Data Transfer Objects) in `Models/DTOs/Dtos.cs` — classes like `CreateBookingRequest`, `LoginResponse`, and `BusSearchResult` — which control exactly what data enters and exits each API endpoint, ensuring that internal fields like `PasswordHash` are never accidentally exposed. Finally, five Service classes (`AuthService`, `BusService`, `BookingService`, `SeatService`, `AdminService`) act as the brain of the system, each owning all business rules for one feature area.

Our project defines three layers of classes:

#### a) Entity Classes — map 1-to-1 with database tables

| File | Class | Maps to DB table |
|------|-------|-----------------|
| `Models/Entities/User.cs` | `User` | `users` |
| `Models/Entities/Admin.cs` | `Admin` | `admins` |
| `Models/Entities/Operator.cs` | `Operator` | `operators` |
| `Models/Entities/Bus.cs` | `Bus` | `buses` |
| `Models/Entities/Booking.cs` | `Booking` | `bookings` |
| `Models/Entities/Seat.cs` | `Seat` | `seats` |
| `Models/Entities/SeatLock.cs` | `SeatLock` | `seat_locks` |

```csharp
// Models/Entities/User.cs
[Table("users")]
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Status { get; set; } = "active";

    // Navigation properties (relationships to other entities)
    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<SeatLock> SeatLocks { get; set; } = [];
}
```

#### b) DTO Classes — define what travels over the API wire

DTOs (Data Transfer Objects) control exactly what data goes in and comes out of each endpoint.

```csharp
// Request DTO — what the client sends
public class CreateBookingRequest
{
    public Guid BusId { get; set; }
    public DateOnly JourneyDate { get; set; }
    public List<PassengerRequest> Passengers { get; set; } = [];
    public PaymentRequest Payment { get; set; } = new();
}

// Response DTO — what the API sends back
public class CreateBookingResponse
{
    public Guid BookingId { get; set; }
    public string BookingStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? TicketDownloadUrl { get; set; }
}
```

#### c) Service Classes — hold the business logic

```csharp
public class AuthService : IAuthService    { ... }
public class BookingService : IBookingService { ... }
public class BusService : IBusService     { ... }
public class AdminService : IAdminService  { ... }
public class SeatService : ISeatService    { ... }
```

### 💡 What This Teaches

Separating classes into **Entities → DTOs → Services** is the Single Responsibility Principle in action. Each class does exactly one job:
- An Entity models a database row.
- A DTO safely shapes data for transport — note `PasswordHash` is in the entity but **never** in any DTO.
- A Service class holds all business rules for a feature.

---

## 2. Getters & Setters (Properties)

### ✅ Already Done

In C#, **properties** replace explicit getter/setter methods with a clean `{ get; set; }` syntax.

In our project, every single piece of data in every entity and DTO is exposed through C# properties rather than raw public fields — there is not a single `public string name;` anywhere in the codebase. This matters because properties give you a hook to add logic later without changing any caller code. We also use the `= default` syntax on setters throughout: `public string Status { get; set; } = "active"` in `User.cs` means every new `User` object starts as active without anyone having to set it explicitly, and `public string Status { get; set; } = "confirmed"` in `Booking.cs` means every booking is confirmed by default. In `Admin.cs`, `public string Role { get; set; } = "admin"` ensures the role is always set at creation. These defaults are business rules baked directly into the model — they cannot be forgotten by the service layer because the property itself enforces them the moment the object is created.

```csharp
// Models/Entities/Bus.cs
public string RegistrationNumber { get; set; } = string.Empty;
public decimal PricePerSeat { get; set; }
public string Status { get; set; } = "active";
public short TotalSeats { get; set; }
```


#### Default values via setters

```csharp
// Booking.cs — Status is set to "confirmed" by default at object creation
public string Status { get; set; } = "confirmed";

// Admin.cs — Role is defaulted to "admin"
public string Role { get; set; } = "admin";
```

### 💡 What This Teaches

Properties give you control over access. If tomorrow you needed to validate that `PricePerSeat` is never negative, you only change:

```csharp
// Before:
public decimal PricePerSeat { get; set; }

// After — validation added to setter, no caller code changes:
private decimal _price;
public decimal PricePerSeat
{
    get => _price;
    set => _price = value < 0
        ? throw new ArgumentException("Price cannot be negative")
        : value;
}
```

Every controller and service that reads `bus.PricePerSeat` stays identical — that's why using properties (not public fields) matters.

---

## 3. Interfaces

### ✅ Already Done — 5 Service Interfaces

An interface is a **contract** — it defines *what* methods must exist without saying *how* they work.

We created five service interfaces that form the backbone of the entire system's architecture. Each interface lives in `Services/Interfaces/` and declares the exact async methods that any implementing class must provide — for example, `IBookingService` declares `CreateBookingAsync`, `GetUserBookingsAsync`, `CancelBookingAsync`, and `GetOperatorBookingsAsync`, but contains zero implementation logic. The concrete classes in `Services/Implementations/` fulfil those contracts: `BookingService` implements every method on `IBookingService` with actual database queries, transaction handling, and business validation. In `Program.cs`, we register every interface-to-implementation pair with ASP.NET Core's built-in Dependency Injection container using `AddScoped<IBookingService, BookingService>()`, and every controller receives the interface type in its constructor — meaning no controller ever directly imports a concrete service class. This strict separation means the entire service layer can be replaced or mocked independently of the controllers.

| Interface | Implemented By | Wired in DI |
|-----------|---------------|-------------|
| `IAuthService` | `AuthService` | ✅ `Program.cs` |
| `IBusService` | `BusService` | ✅ `Program.cs` |
| `IBookingService` | `BookingService` | ✅ `Program.cs` |
| `ISeatService` | `SeatService` | ✅ `Program.cs` |
| `IAdminService` | `AdminService` | ✅ `Program.cs` |

```csharp
// Services/Interfaces/IAuthService.cs — the CONTRACT
public interface IAuthService
{
    Task<(bool Success, string Message, int StatusCode)> RegisterUserAsync(UserRegisterRequest request);
    Task<(bool Success, LoginResponse? Response, string Message, int StatusCode)> LoginUserAsync(LoginRequest request);
    Task<(bool Success, string Message, int StatusCode)> RegisterOperatorAsync(OperatorRegisterRequest request);
    Task<(bool Success, LoginResponse? Response, string Message, int StatusCode)> LoginOperatorAsync(LoginRequest request);
    Task<(bool Success, LoginResponse? Response, string Message, int StatusCode)> LoginAdminAsync(LoginRequest request);
}
```

```csharp
// Services/Implementations/AuthService.cs — FULFILS the contract
public class AuthService : IAuthService
{
    public async Task<(bool Success, string Message, int StatusCode)> RegisterUserAsync(UserRegisterRequest request)
    {
        // actual logic — hashing, DB save, duplicate check
    }
    // ... all 5 methods implemented
}
```

```csharp
// Program.cs — DI wires the interface to the concrete class
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBusService, BusService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ISeatService, SeatService>();
builder.Services.AddScoped<IAdminService, AdminService>();
```

#### Controllers depend on the interface, never the concrete class

```csharp
// AuthController.cs
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;  // ← interface, NOT AuthService

    public AuthController(IAuthService auth) // ← ASP.NET injects the right impl
    {
        _auth = auth;
    }
}
```

### 💡 What This Teaches

The controller doesn't know whether `IAuthService` is backed by `AuthService` or a `MockAuthService`. This is **Dependency Inversion** — you depend on the abstraction, not the detail. In unit tests you swap in a fake without touching a single line of controller code.

---

## 4. Encapsulation

### ✅ Already Done

Encapsulation = **hiding internal details and only exposing what's necessary**.

Encapsulation is one of the strongest aspects of our project's design, and it shows up in at least three distinct places. The most security-critical example is `PasswordHash` — this field exists on the `User`, `Operator`, and `Admin` entity classes so EF Core can persist it to the database, but it is deliberately absent from every single DTO and response class; the `LoginResponse` only carries a JWT token, an expiry, a role, and the email, so there is no path through which the hash could leak to a client. The second place is inside every service class, where the database context (`_db`) and the JWT helper (`_jwt`) are declared `private readonly` — outside code cannot call `authService._db.Users` directly because `_db` is not accessible; every operation must go through the five public methods defined on the interface. The third and most complex example is the transaction inside `BookingService.CreateBookingAsync`: the controller simply calls the method and receives a success or failure tuple; it has no idea that internally a serializable PostgreSQL transaction is being used, seat locks are validated, double-booking is checked, booking details and payment records are inserted, seat locks are removed, and only then the transaction is committed — all of that critical sequencing is completely encapsulated.

#### Example 1 — PasswordHash never leaks to the API response

```csharp
// User entity STORES the hash:
public class User { public string PasswordHash { get; set; } ... }

// LoginResponse sent to the client has NONE of it:
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    // ← No PasswordHash. It's encapsulated inside the service layer.
}
```

#### Example 2 — Private fields in service classes

```csharp
// AuthService.cs
public class AuthService : IAuthService
{
    private readonly BusBookingDbContext _db;  // ← hidden from outside
    private readonly JwtHelper _jwt;           // ← hidden from outside

    // External code can only call the 5 public interface methods.
    // It cannot access _db or _jwt directly.
}
```

#### Example 3 — Transaction logic is completely internal to BookingService

The controller that calls `CreateBookingAsync` has no idea a serializable DB transaction is running internally to prevent double-booking race conditions:

```csharp
// BookingService.cs — caller never sees this complexity
public async Task<...> CreateBookingAsync(Guid userId, CreateBookingRequest request)
{
    using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
    try
    {
        // validate seat locks, check for existing bookings, insert records ...
        await tx.CommitAsync();
    }
    catch { await tx.RollbackAsync(); throw; }
}
```

### 💡 What This Teaches

Encapsulation protects **consistency**. Because `PasswordHash` can only be set inside `AuthService`, there is no chance a bug somewhere else corrupts a user's password. Because the transaction is internal, callers cannot accidentally commit halfway through.

---

## 5. Inheritance

### ✅ Already Done — OperatorDetailResponse extends OperatorListItem

Inheritance lets a child class automatically receive all the properties and behaviour of a parent class, adding only what is unique to itself.

In our project, the admin API needs two operator-related responses: a lightweight list item for the "get all operators" endpoint and a full detail view for the "get one operator" endpoint. Rather than writing the six shared fields (`OperatorId`, `CompanyName`, `Email`, `Phone`, `Status`, `CreatedAt`) twice, we made `OperatorListItem` the base class and `OperatorDetailResponse` the derived class that extends it with just the extra fields (`GstNumber`, `Address`, `RejectionReason`, and the `Buses` list). When `AdminService.GetOperatorByIdAsync` constructs the response, it fills in all ten properties in a single object initialiser — the six inherited ones and the four new ones — without any code repetition. We also use the closely related concept of **generic classes** through `PagedResult<T>`, which is a single class that wraps pagination metadata (`Total`, `Page`, `PageSize`, `Results`) for any result type; the same class is used for bus search results, user booking history, operator lists, and operator booking views without writing four separate wrapper classes.

```csharp
// Models/DTOs/Dtos.cs

public class OperatorListItem                            // BASE class
{
    public Guid OperatorId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class OperatorDetailResponse : OperatorListItem  // DERIVED class
{
    // Inherits all 6 properties above, PLUS adds detail-only properties:
    public string? GstNumber { get; set; }
    public string? Address { get; set; }
    public string? RejectionReason { get; set; }
    public List<BusListItem> Buses { get; set; } = [];
}
```

In `AdminService`, when building the detail response, we set both base and derived properties together:

```csharp
return new OperatorDetailResponse
{
    // Inherited from OperatorListItem:
    OperatorId    = op.Id,
    CompanyName   = op.CompanyName,
    Email         = op.Email,
    Phone         = op.Phone,
    Status        = op.Status,
    CreatedAt     = op.CreatedAt,
    // Own properties of OperatorDetailResponse:
    GstNumber        = op.GstNumber,
    Address          = op.Address,
    RejectionReason  = op.RejectionReason,
    Buses            = op.Buses.Select(b => new BusListItem { ... }).ToList()
};
```

#### Generic class reuse — `PagedResult<T>`

```csharp
public class PagedResult<T>
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<T> Results { get; set; } = [];
}
```

Used across 4 different endpoints without duplication:

```csharp
PagedResult<BusSearchResult>      // bus search
PagedResult<BookingHistoryItem>   // user booking history
PagedResult<OperatorListItem>     // admin operator list
PagedResult<OperatorBookingItem>  // operator booking view
```

### 💡 What This Teaches

Inheritance removes duplication. Without it, `OperatorDetailResponse` would copy-paste all 6 fields from `OperatorListItem`. If you renamed `CompanyName`, you'd update 2 classes instead of 1. Inheritance keeps the codebase **DRY** (Don't Repeat Yourself).

---

## 6. Abstraction

### ✅ Already Done

Abstraction = **showing WHAT something does, hiding HOW it does it**.

Abstraction is applied at every layer of our project. At the controller layer, controllers only know the method names defined on the service interfaces — `_auth.LoginUserAsync(request)` — and have absolutely no knowledge of BCrypt password verification, database query strategy, or JWT token construction; all of that complexity is hidden behind the interface. The `JwtHelper` class is a standalone abstraction: it exposes just `GenerateToken(userId, email, role)` and `GetExpiry()`, while internally it handles HMAC-SHA256 key creation, building a claims array with Subject, Email, Role, and Jti claims, setting expiry, and serialising the final token with `JwtSecurityTokenHandler` — callers write one line, the helper does the heavy lifting. Entity Framework Core provides the most pervasive abstraction in the project: every database operation in every service is written as a C# LINQ expression like `_db.Buses.Where(b => b.Route.SourceCity == source).Include(b => b.Operator)`, and EF Core translates that into optimised SQL at runtime — we never write a raw SQL string anywhere in the service layer. This means the entire persistence strategy is abstracted behind EF Core, and switching databases would require changing only one line in `Program.cs`.

#### Example 1 — Interfaces ARE abstraction

`IAuthService` exposes five method signatures. The controller only needs to know *what* it can call:

```csharp
// Controller only sees the "what":
var result = await _auth.RegisterUserAsync(request);

// The HOW — BCrypt hashing, email duplicate check, DB insert — is hidden in AuthService
```

#### Example 2 — JwtHelper hides all token complexity

```csharp
// Callers simply write:
var token = _jwt.GenerateToken(user.Id, user.Email, "User");
var expiry = _jwt.GetExpiry();

// The 40+ lines of HMAC-SHA256 key setup, claims array building,
// and JwtSecurityTokenHandler.WriteToken are hidden inside JwtHelper
```

```csharp
// Helpers/JwtHelper.cs — complex mechanics are abstracted away
public string GenerateToken(Guid userId, string email, string role)
{
    var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, email),
        new Claim(ClaimTypes.Role, role),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };
    var token = new JwtSecurityToken(issuer, audience, claims,
        expires: DateTime.UtcNow.AddHours(expiryHours), signingCredentials: creds);
    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

#### Example 3 — Entity Framework Core abstracts SQL

```csharp
// We write C# (LINQ):
var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

// EF Core generates and runs the SQL:
// SELECT * FROM users WHERE email = @email LIMIT 1
// We never write raw SQL in services — it's all abstracted.
```

### 💡 What This Teaches

Abstraction lets you **swap internals without breaking callers**. If you switch from PostgreSQL to MySQL, you only change the EF Core provider in `Program.cs`. Every service stays identical because they only talk through EF Core's abstracted API (`_db.Users`, `_db.Bookings`).

---

## 7. Polymorphism

### ⚠️ Partially Done

Polymorphism = **one interface, different behaviours depending on the actual type at runtime**.

Polymorphism appears in our project in three practical forms. The most architecturally important form is **DI-based runtime substitution**: in `Program.cs` we register `AddScoped<IAuthService, AuthService>()`, so when ASP.NET Core creates `AuthController` it injects the concrete `AuthService` — but if we changed that one line to `AddScoped<IAuthService, MockAuthService>()` for a test environment, every controller using `IAuthService` would now get entirely different behaviour (fake tokens, no DB calls) without changing a single line of controller code; the calling code is identical, only the object behind the interface changes. The second form is **generic polymorphism** via `PagedResult<T>`: the same class structure handles pagination for four different data types (`BusSearchResult`, `BookingHistoryItem`, `OperatorListItem`, `OperatorBookingItem`) — the pagination logic is written once but works polymorphically across all of them. The third form is a **switch-expression dispatch** in `BookingService.GetUserBookingsAsync` where the same method body applies a fundamentally different database filter based on the runtime value of `status` — `"upcoming"` filters to future confirmed bookings, `"completed"` includes past journeys, and `"cancelled"` filters cancelled ones, all from a single method entry point.

#### What's already polymorphic

**a) Interface Polymorphism (Runtime Substitution)**

All service interfaces support runtime substitution. The controller code is identical — the behaviour changes based on which concrete type the DI container injects:

```csharp
// Production (Program.cs):
builder.Services.AddScoped<IAuthService, AuthService>();

// Testing (test setup):
builder.Services.AddScoped<IAuthService, MockAuthService>();

// Controller — exactly the same code in both environments:
var result = await _auth.RegisterUserAsync(request);
//                  ↑ calls AuthService in prod, MockAuthService in tests
```

**b) Generic Polymorphism — `PagedResult<T>`**

The same class works with different types; the structure is the same but the data varies:

```csharp
PagedResult<BusSearchResult>     // paginates buses
PagedResult<BookingHistoryItem>  // paginates bookings
PagedResult<OperatorListItem>    // paginates operators
```

**c) Switch-expression Polymorphism — status-based filtering in BookingService**

```csharp
query = status switch
{
    "upcoming"  => query.Where(b => b.Status == "confirmed" && b.JourneyDate >= today),
    "completed" => query.Where(b => b.Status == "completed" || ...),
    "cancelled" => query.Where(b => b.Status == "cancelled"),
    _           => query.Where(b => b.Status == status)
};
```

The *same method* produces different query behaviour based on the value of `status` — a lightweight runtime polymorphism pattern.

#### What could be added — classic `virtual` / `override`

We don't yet use `virtual` method overriding. Here is how it could apply:

```csharp
// A base service with a virtual method
public abstract class BaseService
{
    protected readonly BusBookingDbContext _db;
    protected BaseService(BusBookingDbContext db) => _db = db;

    public virtual string GetServiceName() => "BaseService";
}

// Each concrete service overrides it differently — polymorphism!
public class AuthService : BaseService, IAuthService
{
    public override string GetServiceName() => "AuthService";
}

public class BookingService : BaseService, IBookingService
{
    public override string GetServiceName() => "BookingService";
}

// A single logger call works for all services — same call, different output:
void LogAction(BaseService svc) => Console.WriteLine(svc.GetServiceName());
```

---

## 8. Summary Table

| OOP Concept | Status | Where in BusBookingAPI |
|-------------|--------|----------------------|
| **Classes & Models** | ✅ Done | `Models/Entities/*.cs` · `Models/DTOs/Dtos.cs` · `Services/Implementations/*.cs` |
| **Getters & Setters** | ✅ Done | Every `{ get; set; }` property in all entity and DTO classes |
| **Interfaces** | ✅ Done | `Services/Interfaces/I*.cs` — 5 interfaces, all wired in `Program.cs` DI |
| **Encapsulation** | ✅ Done | Private `_db`/`_jwt` fields · PasswordHash hidden from DTOs · Transaction internal to BookingService |
| **Inheritance** | ✅ Done | `OperatorDetailResponse : OperatorListItem` · Generic `PagedResult<T>` |
| **Abstraction** | ✅ Done | `IAuthService` hides impl · `JwtHelper` hides token mechanics · EF Core hides SQL |
| **Polymorphism** | ⚠️ Partial | DI interface substitution ✅ · Generics ✅ · `virtual`/`override` ❌ not yet used |

---

