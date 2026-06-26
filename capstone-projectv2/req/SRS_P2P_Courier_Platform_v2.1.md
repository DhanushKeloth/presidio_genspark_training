# Software Requirements Specification & Requirements Understanding Document
## Peer-to-Peer Courier Service Platform
**Version:** 2.1 | **Status:** Approved for Development | **Date:** June 2026

> **Changelog from v2.0:**
> - Removed refresh token system — JWT-only stateless authentication for capstone
> - FR0.4 rewritten; FR0.7 and FR0.8 deleted
> - Removed `/api/auth/refresh` endpoint from API contract and all role function tables
> - Removed `refreshToken` from login response; updated logout to client-side discard
> - Removed NFR1 bullet about refresh token hashing
> - Removed `RefreshTokens` entity (Section 8.4) and its ERD relationship
> - Corrected Shipments entity — flat OTP and address columns extracted into child tables (were already wrong in v2.0 vs the DB schema)
> - Added `ShipmentAddresses` entity (Section 8.5) — was missing entirely
> - Added `ShipmentOtpWindows` entity (Section 8.6) — was missing entirely
> - Updated Phase 1 roadmap to remove refresh token implementation steps
> - Updated Risk Register JWT/XSS row to reflect stateless token approach

---

## Table of Contents

1. [Document Purpose & Scope](#1-document-purpose--scope)
2. [Requirements Understanding](#2-requirements-understanding)
3. [System Overview](#3-system-overview)
4. [Technical Architecture](#4-technical-architecture)
5. [User Roles, Responsibilities & Functions](#5-user-roles-responsibilities--functions)
6. [Functional Requirements](#6-functional-requirements)
7. [Non-Functional Requirements](#7-non-functional-requirements)
8. [Data Models & Entity Design](#8-data-models--entity-design)
9. [API Contract](#9-api-contract)
10. [System Workflows](#10-system-workflows)
11. [SignalR Real-Time Event Contract](#11-signalr-real-time-event-contract)
12. [State Machine](#12-state-machine)
13. [Implementation Roadmap](#13-implementation-roadmap)
14. [Risk Register](#14-risk-register)

---

## 1. Document Purpose & Scope

### 1.1 Purpose

This document serves two functions simultaneously:

**As an SRS:** It defines the complete, unambiguous functional and non-functional requirements for the Peer-to-Peer Courier Service Platform — a B2C logistics application modelled after services like Rapido Parcel Delivery. It is the authoritative reference for all design, development, testing, and evaluation activities.

**As a Requirements Understanding Document:** It captures the business logic, user intent, and reasoning behind every requirement — explaining not just what to build but why, so that any developer picking up this document can implement the system without ambiguity.

### 1.2 Project Context

The platform replicates the model of a peer-to-peer parcel courier service. Unlike enterprise freight networks which use multi-terminal relay chains, this system works on a direct model:

- A **Sender (Customer)** books a parcel pickup from their location.
- An **available Driver** accepts the job, navigates to the Sender, verifies their identity via a Pickup OTP, collects the parcel, and drives to the Recipient's address.
- The **Recipient** verifies the delivery via a Delivery OTP before the Driver hands over the parcel.
- Throughout the journey, both Sender and Recipient can track the Driver's live location.
- The **Admin** monitors all operations, manages the driver roster, and can intervene in edge cases.

### 1.3 Scope Boundaries

**In scope:**
- Customer account self-registration and booking
- Driver registration with admin approval gate
- Two-OTP secure handover flow (Pickup OTP + Delivery OTP)
- Real-time GPS simulation via SignalR WebSockets
- Admin driver lifecycle management and platform monitoring
- JWT authentication (stateless — access token only; no refresh token for capstone)
- Multi-item shipment booking

**Out of scope (deferred to future versions):**
- Payment gateway integration
- Driver rating and review system
- Fleet and vehicle management module
- Email or SMS notifications (OTP delivered only via in-app SignalR)
- Multi-city multi-leg relay logistics
- Native mobile applications (web only)
- Refresh token rotation and multi-device session management

### 1.4 Definitions

| Term | Definition |
|------|------------|
| Sender | The Customer who initiates the booking and hands the parcel to the Driver |
| Recipient | The end-destination person — may be the same or different person as the Sender |
| Pickup OTP | A 4-digit code verified by the Sender before the Driver collects the parcel |
| Delivery OTP | A separate 4-digit code verified by the Recipient before the Driver delivers the parcel |
| TrackingNumber | System-generated alphanumeric identifier, e.g. TRK-A3X9B1 |
| POD | Proof of Delivery — the Delivery OTP verification that closes the shipment |
| POP | Proof of Pickup — the Pickup OTP verification that starts transit |
| SignalR Group | A named WebSocket broadcast channel scoped to one shipment's TrackingNumber |
| Background Service | An IHostedService in .NET that runs GPS coordinate simulation on a timer |
| State Machine Guard | Service-layer validation that rejects invalid shipment status transitions |

---

## 2. Requirements Understanding

### 2.1 Understanding the Core Business Flow

The fundamental insight in this system is that there are two identity verification points, not one.

**At pickup:** The Driver goes to the Sender's address. The Sender receives an OTP on their screen. They read it to the Driver. The Driver submits it. This proves the parcel was collected from the correct person.

**At delivery:** The Driver reaches the Recipient's address. The Recipient receives an OTP on their screen. They read it to the Driver. The Driver submits it. This proves the parcel was handed to the correct person.

This dual-OTP flow creates a cryptographic chain of custody — you can prove the parcel left the right hands and arrived in the right hands.

### 2.2 Why Two Separate OTPs?

The reason for generating two independent OTPs rather than one OTP used twice is:

- **Security isolation:** The Pickup OTP proves Sender identity. The Delivery OTP proves Recipient identity. If both used the same code, a compromised Sender could pre-share the code with anyone.
- **Timing independence:** The pickup happens potentially hours before delivery. A single OTP with a 15-minute window cannot span the full transit time.
- **Separate audit records:** Each OTP verification creates a timestamped ShipmentEvent. The business needs to prove when the parcel changed hands, not just that it eventually arrived.

### 2.3 Understanding the Tracking Model

The live tracking serves three distinct purposes:

1. **Customer reassurance** — the Sender and Recipient can see the Driver's pin moving on the map without refreshing.
2. **ETA estimation** — the frontend can calculate approximate arrival time from current coordinates vs destination.
3. **Dispute resolution** — the ShipmentEvents log with coordinate snapshots creates an immutable timeline. If a parcel goes missing, the last known location is on record.

The GPS movement is **simulated** in this system — a .NET Background Service interpolates coordinates between the pickup address and dropoff address at a configurable interval. In production, this would be replaced by actual GPS updates from a driver mobile app.

### 2.4 Understanding the Driver Approval Gate

Drivers cannot operate until an Admin activates their account. This gate exists because:

- A Driver physically enters a Sender's premises to collect a parcel. The platform has a duty of care to verify that drivers are legitimate before granting them access to customer addresses.
- The PendingApproval default state means a Driver can register but is blocked from viewing the job queue, self-assigning, or performing any delivery action until approved.
- This is modelled after how Rapido, Dunzo, and similar services operate — driver onboarding always has a verification checkpoint.

### 2.5 Understanding the Race Condition Problem

When multiple Drivers view the pending queue simultaneously, they may all attempt to claim the same shipment at the same instant. Without protection, two Drivers could both successfully assign themselves to the same parcel.

The mitigation is an EF Core database transaction with a row-level read lock: when a Driver hits the assign endpoint, the system begins a transaction, reads the shipment row with a pessimistic lock, checks that status = Pending, updates to Assigned, and commits. Any concurrent transaction attempting the same read is blocked until the first commits, then sees Assigned and returns HTTP 409 Conflict.

### 2.6 Understanding Why GPS Lives on DriverProfile, Not Shipment

A common design mistake is to store current_lat and current_lng on the Shipment entity. This is wrong because a shipment does not move — the Driver moves. The parcel's current location is the Driver's current location. The Background Service updates the Driver's position, and SignalR reads it and broadcasts to all subscribers of that shipment's group.

Coordinate snapshots at key events (PickedUp, InTransit, Arrived) are stored in ShipmentEvents for the GPS breadcrumb trail.

### 2.7 Understanding the JWT-Only Authentication Approach

For this capstone, authentication is stateless — the server issues a signed JWT on login and does not store any token state in the database. This means:

- **No refresh token table** — there is no `RefreshTokens` DB table to scaffold, seed, or query.
- **Logout is client-side** — the server has no record to revoke. The Angular AuthService discards the in-memory token on logout.
- **Token expiry ends the session** — when the JWT expires (default 15 minutes), the user must log in again. There is no silent renewal.
- **Acceptable tradeoff for capstone** — the stateless approach eliminates an entire auth subsystem (rotation logic, hash storage, revocation checks) with no loss of demonstrable functionality for evaluation purposes.

---

## 3. System Overview

### 3.1 Platform Description

A B2C web application providing end-to-end parcel delivery management. The system facilitates direct peer-to-peer courier service: a Sender books a parcel, a Driver collects it via Pickup OTP verification, transports it with live GPS tracking, and delivers it to the Recipient via Delivery OTP verification.

### 3.2 Delivery Lifecycle at a Glance

```
Sender books shipment                          [status: Pending]
    |
Driver views job queue and self-assigns        [status: Assigned]
    |
Driver navigates to Sender's address
    |
System generates Pickup OTP -> pushed to Sender's screen
    |
Sender reads OTP to Driver -> Driver submits it
    |                                          [status: PickedUp — POP confirmed]
Driver starts driving to Recipient
    |                                          [status: InTransit — GPS begins]
Real-time coordinates pushed to all tracking screens
    |
Driver reaches Recipient's address             [status: Arrived]
    |
System generates Delivery OTP -> pushed to Recipient's screen
    |
Recipient reads OTP to Driver -> Driver submits it
    |                                          [status: Delivered — POD confirmed]
Both screens show delivery success
```

---

## 4. Technical Architecture

### 4.1 Stack

| Layer | Technology | Version |
|-------|------------|---------|
| Frontend | Angular + TypeScript | Angular 17+ |
| Backend API | ASP.NET Core Web API (C#) | .NET 8 |
| ORM | Entity Framework Core (DB-First) | EF Core 8 |
| Database | PostgreSQL | 15+ |
| Real-Time | ASP.NET Core SignalR | Built-in .NET 8 |
| Authentication | JWT Bearer (stateless) | Microsoft.AspNetCore.Authentication.JwtBearer |
| Background Jobs | IHostedService / BackgroundService | Built-in .NET 8 |

### 4.2 Backend Layer Structure

```
Controllers/        HTTP entry points, request validation, routing
Services/           Business logic, state machine guards, OTP generation
Repositories/       EF Core data access, query abstraction
Hubs/               SignalR WebSocket hub (TrackingHub)
BackgroundServices/ GPS simulation timer (GpsSimulationService)
Models/             EF Core entity classes (scaffolded from DB schema)
DTOs/               Request and response transfer objects
Middleware/         Global exception handler
Enums/              ShipmentStatus, UserRole, DriverAccountStatus, DriverOpStatus,
                    AddressType, OtpType
```

### 4.3 Frontend Module Structure

```
src/app/
  auth/        Login, register forms (Customer + Driver)
  customer/    Booking form, shipment list, tracking page
  driver/      Job queue, active job screen, OTP submission
  admin/       Driver management, platform dashboard
  shared/      SignalRService, AuthGuard, HTTP interceptors, models
```

### 4.4 Development Environment

Run all three services natively without Docker to preserve RAM on an 8GB machine:
- Angular frontend: `ng serve` on port 4200
- .NET API: `dotnet run` on port 5000
- PostgreSQL: running as a local system service on port 5432

---

## 5. User Roles, Responsibilities & Functions

### 5.1 Customer (Sender / Recipient)

#### 5.1.1 Role Definition

The Customer operates in two capacities:
- As a **Sender**: books the shipment, provides pickup address, receives the Pickup OTP, and hands the parcel to the Driver.
- As a **Recipient**: provides the delivery address at booking, receives the Delivery OTP, and accepts the parcel from the Driver.

The Sender and Recipient may be the same person (self-delivery) or different people. The system distinguishes them via contact_name, contact_phone, and the Dropoff address row in the ShipmentAddresses child table.

#### 5.1.2 Account Lifecycle

- Self-registers with email, password, and full name.
- No admin approval required — account is active immediately.
- JWT issued on successful login with role claim "Customer".

#### 5.1.3 Responsibilities

- Provide accurate pickup and dropoff addresses and recipient contact details at booking.
- Declare correct item details — description, weight, dimensions — for each package.
- Be physically present at the pickup location when the Driver arrives.
- Receive the Pickup OTP on their tracking screen and read it aloud to the Driver.
- Ensure the Recipient is reachable and available at the dropoff address.
- The Recipient must read the Delivery OTP to the Driver on arrival.
- Act within OTP windows (15 minutes) to avoid delivery delays.

#### 5.1.4 Accessible Features

| Feature | Access Level | Notes |
|---------|--------------|-------|
| Register / Login | Full | Self-service, no approval gate |
| Book shipment | Full | Multi-item form with recipient details |
| View own shipment history | Own records only | Paginated, filterable by status |
| Track by TrackingNumber | Public (no login needed) | Anyone with the number can track |
| Live tracking screen via SignalR | Full | Real-time GPS pin + event timeline |
| Receive Pickup OTP | Sender only | Pushed via SignalR on Driver's request |
| Receive Delivery OTP | Recipient only | Pushed via SignalR on Driver's request |
| View shipment event timeline | Own records only | Chronological status history |

#### 5.1.5 Blocked Actions

- Cannot view other customers' shipments.
- Cannot submit OTPs — submission is a Driver-only action.
- Cannot modify a shipment after a Driver has self-assigned it.
- Cannot access Driver or Admin endpoints.
- Cannot directly change shipment status.

#### 5.1.6 Functions (API Calls)

| Function | Method | Endpoint | Description |
|----------|--------|----------|-------------|
| Register | POST | /api/auth/register | Creates User (role=Customer). Password hashed. JWT issued. |
| Login | POST | /api/auth/login | Validates credentials. Returns access JWT only. |
| Logout | POST | /api/auth/logout | Client discards in-memory token. Server is stateless — no DB write. |
| Book shipment | POST | /api/shipments | Creates Shipment + ShipmentItems + ShipmentAddresses rows. Generates TrackingNumber. Status = Pending. |
| List own shipments | GET | /api/shipments | Returns paginated own shipments. Supports ?status= and ?page= filters. |
| Get shipment detail | GET | /api/shipments/{id} | Returns full shipment + items + event timeline. Ownership enforced. |
| Track by number | GET | /api/track/{trackingNumber} | Public. Returns status, location, event timeline. OTP never exposed. |
| Join SignalR group | WS | TrackingHub: JoinShipmentGroup | Subscribes to GPS and OTP push events for a shipment. |

---

### 5.2 Driver (Courier)

#### 5.2.1 Role Definition

The Driver is the physical executor of the delivery. They collect the parcel from the Sender (verified via Pickup OTP) and deliver it to the Recipient (verified via Delivery OTP). They own the highest number of system state transitions of any role.

#### 5.2.2 Account Lifecycle

- Registers with email, password, vehicle type, and license number.
- Account defaults to PendingApproval — cannot perform any job-related operations.
- Admin reviews the profile and sets account_status = Active.
- Once Active, the Driver can toggle availability and accept jobs.

#### 5.2.3 Shipment Status Transitions Owned by Driver

```
Pending      --(self-assign)--------------> Assigned
Assigned     --(Pickup OTP verified)-------> PickedUp
PickedUp     --(start transit)-------------> InTransit
InTransit    --(arrive at destination)-----> Arrived
Arrived      --(Delivery OTP verified)-----> Delivered
```

#### 5.2.4 Responsibilities

- Register a complete driver profile with valid vehicle and license details.
- Only toggle status to Available when genuinely ready to accept a job.
- Navigate to the Sender's address after self-assigning.
- Request the Pickup OTP and verify it with the Sender before collecting the parcel.
- Start transit and drive to the Recipient's address.
- Trigger Arrived only when physically at the delivery address.
- Request the Delivery OTP and verify it with the Recipient to confirm handover.
- If an OTP window expires, request regeneration — never attempt to bypass verification.
- Maximum 3 OTP submission attempts per window; after that, regeneration is required.

#### 5.2.5 Accessible Features

| Feature | Access Level | Notes |
|---------|--------------|-------|
| Register driver profile | Full | Defaults to PendingApproval |
| Toggle operational status | Active drivers only | Available, InTransit, Offline |
| View pending job queue | Active drivers only | All Pending shipments, no sensitive customer data |
| Self-assign a shipment | Active + Available only | Atomic with race condition protection |
| Request Pickup OTP generation | Assigned driver only | Triggers OTP push to Sender |
| Submit Pickup OTP | Assigned driver only | Max 3 attempts |
| Progress status: PickedUp to InTransit to Arrived | Assigned driver only | State machine enforced |
| Request Delivery OTP generation | Arrived driver only | Triggers OTP push to Recipient |
| Submit Delivery OTP | Arrived driver only | Max 3 attempts |
| Regenerate OTP (either type) | On expiry only | Resets 15-minute window and attempt count |

#### 5.2.6 Blocked Actions

- Cannot operate at all until Admin approves the account.
- Cannot view or claim a shipment assigned to another Driver.
- Cannot skip status transitions — enforced by state machine guard.
- Cannot see the OTP values — they are only pushed to Sender/Recipient screens.
- Cannot access customer account data beyond what is in the assigned shipment.
- Cannot access Admin endpoints.
- Cannot submit more than 3 OTP attempts per window.

#### 5.2.7 Functions (API Calls)

| Function | Method | Endpoint | Description |
|----------|--------|----------|-------------|
| Register | POST | /api/auth/register | Creates User (role=Driver) + DriverProfile. account_status = PendingApproval. |
| Login | POST | /api/auth/login | Same endpoint. JWT contains role=Driver claim. |
| Toggle operational status | PUT | /api/drivers/op-status | Updates DriverProfile.op_status. Validates Active account status. |
| View pending queue | GET | /api/shipments/queue | All Pending shipments. Active drivers only. Paginated. |
| Self-assign shipment | PUT | /api/shipments/{id}/assign | EF Core transaction + row lock. Returns 409 if already claimed. |
| Request Pickup OTP | POST | /api/shipments/{id}/request-pickup-otp | Generates 4-digit OTP, 15-min expiry, pushes to Sender via SignalR. |
| Submit Pickup OTP | POST | /api/shipments/{id}/verify-pickup-otp | Validates OTP, expiry, attempt count. On success: status = PickedUp. |
| Update status | PUT | /api/shipments/{id}/status | For PickedUp to InTransit and InTransit to Arrived transitions. |
| Request Delivery OTP | POST | /api/shipments/{id}/request-delivery-otp | Generates delivery OTP, pushes to Recipient via SignalR. Status must be Arrived. |
| Submit Delivery OTP | POST | /api/shipments/{id}/verify-delivery-otp | Validates delivery OTP. On success: status = Delivered, delivered_at = UTC now. |
| Regenerate OTP | POST | /api/shipments/{id}/regenerate-otp | Resets code + expiry + attempt count. Pushes new OTP to Sender or Recipient. |

---

### 5.3 Admin (Dispatcher / Operator)

#### 5.3.1 Role Definition

The Admin is the platform operator. They do not participate in any delivery. Their function is to control who can operate as a Driver and to monitor platform health. Admin accounts are pre-seeded in the database at deployment — there is no self-registration path for this role.

#### 5.3.2 Responsibilities

- Review newly registered Driver profiles and approve or reject them.
- Suspend Drivers who violate platform rules or present safety concerns.
- Reactivate suspended Drivers after review.
- Soft-delete Driver accounts while preserving historical shipment data.
- Monitor platform-wide shipment health — identify stuck or delayed deliveries.
- Perform status overrides when the system reaches an inconsistent state.
- Review platform metrics: delivery rates, pending approvals, active shipments.

#### 5.3.3 Accessible Features

| Feature | Access Level | Notes |
|---------|--------------|-------|
| View all driver profiles | Full | With filters by account_status |
| Approve driver | Full | Sets account_status = Active, records approved_at |
| Suspend driver | Full | Immediate; warns if driver has active shipment |
| Reactivate suspended driver | Full | Sets back to Active |
| Soft-delete driver | Full | is_active = FALSE; all data preserved |
| View all shipments platform-wide | Full | Not scoped to any customer |
| Override shipment status | Full | Bypasses state machine guard |
| Dashboard metrics | Full | Counts by status, approvals, delivery rate |

#### 5.3.4 Blocked Actions

- Cannot self-register — pre-seeded accounts only.
- Cannot see OTP values of any shipment.
- Cannot assign shipments to Drivers — Drivers self-assign.
- Cannot modify customer accounts or personal data.
- Cannot book shipments on behalf of customers.

#### 5.3.5 Functions (API Calls)

| Function | Method | Endpoint | Description |
|----------|--------|----------|-------------|
| Login | POST | /api/auth/login | Same endpoint. JWT contains role=Admin. |
| List all drivers | GET | /api/admin/drivers | All DriverProfile rows. Supports ?status= filter. Paginated. |
| Get driver detail | GET | /api/admin/drivers/{id} | Full profile + shipment history + approval metadata. |
| Update driver account status | PUT | /api/admin/drivers/{id}/status | Sets Active, Suspended, or Deleted. Warns on active shipment. |
| List all shipments | GET | /api/admin/shipments | Platform-wide. Filters: status, driver, date range. |
| Get shipment detail | GET | /api/admin/shipments/{id} | Full detail including all events and items. |
| Override shipment status | PUT | /api/admin/shipments/{id}/override-status | Force-update status. Inserts ShipmentEvent with "Admin override". |
| Dashboard metrics | GET | /api/admin/dashboard | Counts: shipments by status, drivers by status, today's deliveries, pending approvals. |

---

## 6. Functional Requirements

### Module 0 — Authentication & Authorization

| ID | Requirement | Priority |
|----|-------------|----------|
| FR0.1 | The system shall allow users to register as Customer or Driver with email, password, and full name. Driver registration additionally requires vehicle type and license number. | Must Have |
| FR0.2 | Passwords shall be hashed using ASP.NET Core Identity PasswordHasher before persistence. Plain-text passwords must never be stored or logged at any point. | Must Have |
| FR0.3 | The system shall issue a signed JWT access token on successful login, embedding the user's id, email, role, and name as claims. Access token expiry shall be configurable and default to 15 minutes. | Must Have |
| FR0.4 | The system shall use stateless JWT authentication only. No refresh token is issued and no token state is stored in the database. When the access token expires, the user must re-authenticate via the login endpoint. This is an intentional capstone simplification — refresh token rotation is deferred to a future version. | Must Have |
| FR0.5 | All API endpoints except /api/auth/register, /api/auth/login, and /api/track/{trackingNumber} shall require a valid JWT. Requests without a valid token shall receive HTTP 401 Unauthorized. | Must Have |
| FR0.6 | Role-based access shall be enforced at the controller level. Accessing a route without the required role shall return HTTP 403 Forbidden. | Must Have |

---

### Module 1 — Shipment Booking

| ID | Requirement | Priority |
|----|-------------|----------|
| FR1.1 | Authenticated Customers shall submit a booking request containing: pickup address, optional pickup coordinates, dropoff address, optional dropoff coordinates, recipient name, recipient phone, and one or more shipment items each with description, weight, length, width, height, and quantity. | Must Have |
| FR1.2 | The API shall generate a unique alphanumeric TrackingNumber in the format TRK- followed by 6 random uppercase alphanumeric characters upon successful booking. Uniqueness shall be validated against existing records before finalisation. | Must Have |
| FR1.3 | The system shall set the initial shipment status to Pending and insert a ShipmentEvent row with description "Shipment booked" and occurred_at = UTC now. | Must Have |
| FR1.4 | The booking shall be rejected if the same Customer submits an identical pickup and dropoff address pair within 60 seconds. The API shall return HTTP 409 Conflict with a descriptive message as an idempotency guard. | Should Have |
| FR1.5 | Customers shall retrieve a paginated list of their own shipments, supporting ?status= filter and ?page= / ?size= query parameters. Maximum page size is 50. | Must Have |
| FR1.6 | Customers shall retrieve full detail of any shipment they own, including all ShipmentItems, both ShipmentAddresses rows, and the complete ShipmentEvents timeline ordered by occurred_at ascending. | Must Have |
| FR1.7 | The public tracking endpoint /api/track/{trackingNumber} shall return: current status, current driver location if InTransit, and the full event timeline. The OTP codes, recipient phone, and internal database IDs shall never be exposed on this endpoint. | Must Have |

---

### Module 2 — Driver Management

| ID | Requirement | Priority |
|----|-------------|----------|
| FR2.1 | Drivers shall complete a registration form with email, password, full name, vehicle type, and license number. The system shall create a User row with role=Driver and a DriverProfile row with account_status = PendingApproval. | Must Have |
| FR2.2 | A Driver with account_status of PendingApproval or Suspended shall be blocked from accessing the job queue, self-assigning shipments, toggling operational status, or performing any delivery action. The API shall return HTTP 403 with message "Account not active". | Must Have |
| FR2.3 | Admins shall update a Driver's account_status to Active, Suspended, or Deleted. On transition to Suspended where the Driver has a shipment currently InTransit or Arrived, the service layer shall return a warning alongside the successful update. | Must Have |
| FR2.4 | Active Drivers shall toggle their op_status between Available, InTransit, and Offline. Transitioning to Available from InTransit shall be blocked if the Driver has an active non-Delivered assigned shipment. | Must Have |
| FR2.5 | Active Available Drivers shall view a paginated queue of all Pending shipments showing: tracking number, pickup address area, dropoff address area, total weight, and item count. Full customer name and phone details shall not be visible in the queue view. | Must Have |
| FR2.6 | A Driver shall self-assign a Pending shipment using a database transaction with row-level locking. On successful assignment, driver_id is set, status becomes Assigned, and a ShipmentEvent row is inserted. If the shipment was already claimed by another driver, the API shall return HTTP 409 Conflict. | Must Have |

---

### Module 3 — Pickup OTP and Proof of Pickup

| ID | Requirement | Priority |
|----|-------------|----------|
| FR3.1 | Once a Driver has self-assigned a shipment with status=Assigned, they shall trigger a Request Pickup OTP action. The backend shall generate a cryptographically random 4-digit numeric OTP, store it in the ShipmentOtpWindows row (otp_type=Pickup) with expires_at = UTC now + 15 minutes, and broadcast it to the Sender's live tracking session via SignalR. | Must Have |
| FR3.2 | The Sender's tracking screen shall display the Pickup OTP prominently with a visible countdown timer showing the remaining validity window. | Must Have |
| FR3.3 | The Driver shall enter the OTP verbally obtained from the Sender. The backend shall validate: OTP matches, not expired, attempt count under 3. On success: status = PickedUp, picked_up_at = UTC now, OTP code cleared (set NULL), verified_at set, ShipmentEvent inserted, SignalR broadcast sent. | Must Have |
| FR3.4 | On each failed OTP attempt, attempt_count on the ShipmentOtpWindows row shall increment. At 3 failed attempts, the endpoint shall return HTTP 429 with message "Maximum attempts reached. Please regenerate the OTP." | Must Have |
| FR3.5 | If the Pickup OTP window expires, the Driver shall call the regenerate endpoint. A new OTP shall be generated, expires_at reset to UTC now + 15 minutes, and attempt_count reset to 0. The new OTP shall be pushed to the Sender via SignalR. | Must Have |

---

### Module 4 — Transit and Real-Time Tracking

| ID | Requirement | Priority |
|----|-------------|----------|
| FR4.1 | Once a Driver confirms pickup with status=PickedUp, they shall trigger a Start Transit action updating status to InTransit. The Driver's op_status shall automatically update to InTransit. A ShipmentEvent with current coordinates shall be inserted. | Must Have |
| FR4.2 | A .NET BackgroundService shall run on a configurable interval defaulting to every 5 seconds. For each InTransit shipment it shall increment the assigned Driver's current_lat and current_lng along the interpolated route from pickup coordinates to dropoff coordinates. | Must Have |
| FR4.3 | After updating coordinates, the Background Service shall call IHubContext of TrackingHub to broadcast a LocationUpdated event to the shipment's SignalR group containing: trackingNumber, latitude, longitude, and timestamp. | Must Have |
| FR4.4 | The Angular frontend shall connect to the SignalR hub on tracking page load, join the shipment's group by trackingNumber, and update the map pin or progress indicator on LocationUpdated events without a page refresh. | Must Have |
| FR4.5 | The Angular frontend shall implement automatic SignalR reconnection logic with exponential backoff on connection drops. Maximum reconnect wait time shall not exceed 30 seconds. | Should Have |
| FR4.6 | Both the Sender and the Recipient shall be able to join the same SignalR group for a shipment and receive identical location updates. | Must Have |

---

### Module 5 — Delivery OTP and Proof of Delivery

| ID | Requirement | Priority |
|----|-------------|----------|
| FR5.1 | When the Driver reaches the Recipient's address, they shall trigger an Arrived status update. The backend shall set status = Arrived, insert a ShipmentEvent with current coordinates, and broadcast a DriverArrived event via SignalR to the shipment's group. | Must Have |
| FR5.2 | The Driver shall then trigger Request Delivery OTP. The backend shall generate a cryptographically random 4-digit numeric OTP, store it in the ShipmentOtpWindows row (otp_type=Delivery) with expires_at = UTC now + 15 minutes, and broadcast it to the Recipient's live tracking session via SignalR. | Must Have |
| FR5.3 | The Recipient's tracking screen shall display the Delivery OTP with a visible countdown timer. | Must Have |
| FR5.4 | The Driver shall enter the Delivery OTP verbally obtained from the Recipient. The backend shall validate: OTP matches, not expired, attempt count under 3. On success: status = Delivered, delivered_at = UTC now, OTP code cleared (set NULL), verified_at set, ShipmentEvent inserted. | Must Have |
| FR5.5 | On successful delivery, the backend shall broadcast a ShipmentDelivered SignalR event to the group. Both Sender and Recipient tracking screens shall display a delivery success state and leave the SignalR group. | Must Have |
| FR5.6 | On each failed Delivery OTP attempt, attempt_count on the ShipmentOtpWindows (Delivery) row shall increment. At 3 failures the API shall return HTTP 429 requiring regeneration before further attempts. | Must Have |
| FR5.7 | If the Delivery OTP window expires, the Driver shall call the regenerate endpoint. A new OTP is generated, expires_at reset, attempt_count reset to 0, and new OTP pushed to Recipient via SignalR. | Must Have |

---

### Module 6 — Admin Operations

| ID | Requirement | Priority |
|----|-------------|----------|
| FR6.1 | Admins shall view all Driver profiles with filters: account_status (PendingApproval, Active, Suspended, Deleted), pagination, and sort by registration date. | Must Have |
| FR6.2 | Admins shall update a Driver's account_status to any valid value. The approved_at timestamp and approved_by (admin user id) shall be recorded when transitioning to Active. | Must Have |
| FR6.3 | Admins shall view all platform shipments with filters for status, driver assignment, and date range. Response includes customer name, driver name, tracking number, status, and timestamps. | Must Have |
| FR6.4 | Admins shall override any shipment's status, bypassing the state machine guard. A ShipmentEvent row with description "Status overridden by Admin" and actor_id = admin.id shall be inserted on every override. | Must Have |
| FR6.5 | The Admin dashboard endpoint shall return: total shipments by status as counts, drivers by account_status as counts, shipments delivered today, and pending driver approvals count. | Must Have |

---

## 7. Non-Functional Requirements

### NFR1 — Security

- All endpoints except register, login, and public track shall require JWT. Unauthenticated requests return HTTP 401.
- Role claims shall be strictly validated per endpoint. Cross-role access returns HTTP 403.
- OTP values shall never appear in API logs, error responses, or the public tracking endpoint.
- JWT signing secret shall be stored in environment variables or appsettings.json secrets — never hardcoded.
- OTP submission shall be rate-limited to 3 attempts per window enforced via attempt_count in the ShipmentOtpWindows table.

### NFR2 — Real-Time Performance

- SignalR LocationUpdated push events from Background Service to connected clients shall complete in under 500ms under normal load on a single-machine development setup.
- The Background Service GPS timer shall use await Task.Delay (non-blocking) never Thread.Sleep (blocking). It must run on a background thread and must not block the ASP.NET Core request pipeline.
- SignalR groups shall be scoped by TrackingNumber to prevent broadcasting location data to unrelated clients.

### NFR3 — Data Integrity

- Driver self-assignment shall execute within an EF Core transaction using pessimistic locking. Concurrent claims on the same shipment shall never both succeed.
- Shipment status transitions shall be validated against the state machine before any DB write. Invalid transitions return HTTP 409 Conflict.
- All status transitions shall insert a ShipmentEvent row in the same transaction as the status update. Partial writes where status updates but no event row is created are unacceptable.

### NFR4 — Error Handling

A global exception handler middleware shall intercept all unhandled exceptions and return structured JSON responses. No stack traces shall be exposed in production. Standard HTTP codes apply: 400 for validation errors, 401 for missing or invalid JWT, 403 for insufficient role, 404 for resource not found, 409 for conflict or state machine violation, 429 for OTP attempts exceeded, 500 for unexpected server errors.

### NFR5 — Observability

- ILogger shall log at Information level: user registration, login, shipment booked, driver assigned, OTP requested (type only not value), OTP verified, shipment delivered.
- ILogger shall log at Warning level: failed OTP attempt with count, Admin override action.
- OTP codes shall never appear in log output at any level.

### NFR6 — Scalability Baseline

- The Background Service shall query only InTransit shipments — never all shipments — on each timer tick.
- All list endpoints shall be paginated with a configurable maximum page size.
- Database indexes shall cover: shipments.tracking_number (unique), shipments.customer_id, shipments.driver_id, shipments.status, shipment_events.shipment_id, shipment_otp_windows.shipment_id, shipment_addresses.shipment_id, users.email (unique).

---

## 8. Data Models & Entity Design

### 8.1 Entity Relationship Summary

```
Users ──1:1──► CustomerProfiles       (user_id UNIQUE FK)
Users ──1:1──► DriverProfiles         (user_id UNIQUE FK)
CustomerProfiles ──1:N──► SavedAddresses  (customer_id FK)
Users ──1:N──► Shipments              (customer_id FK, NOT NULL)
Users ──1:N──► Shipments              (driver_id FK, NULLABLE)
Shipments ──1:N──► ShipmentAddresses  (ON DELETE CASCADE; exactly 2 rows: Pickup + Dropoff)
Shipments ──1:N──► ShipmentItems      (ON DELETE CASCADE)
Shipments ──1:N──► ShipmentOtpWindows (ON DELETE CASCADE; up to 2 rows: Pickup + Delivery)
Shipments ──1:N──► ShipmentEvents     (ON DELETE CASCADE; append-only)
```

### 8.2 Users

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| id | INT | PK, auto-increment | |
| email | CITEXT | NOT NULL, UNIQUE | Case-insensitive. Login identifier |
| full_name | VARCHAR(100) | NOT NULL | Authoritative display name for all roles |
| password_hash | TEXT | NOT NULL | ASP.NET Identity PasswordHasher output |
| role | ENUM | NOT NULL | Customer, Driver, Admin |
| is_active | BOOLEAN | NOT NULL, DEFAULT TRUE | Soft-delete flag |
| created_at | TIMESTAMPTZ | NOT NULL | UTC |
| updated_at | TIMESTAMPTZ | NOT NULL | Auto-set by trigger on update |

Indexes: uq_users_email (implicit unique), idx_users_role

### 8.3 CustomerProfiles

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| id | INT | PK, auto-increment | |
| user_id | INT | FK Users, NOT NULL, UNIQUE | UNIQUE enforces 1:1 relationship |
| phone_number | VARCHAR(20) | NULLABLE | Primary contact |
| alternate_phone_number | VARCHAR(20) | NULLABLE | Backup contact |
| profile_image_url | TEXT | NULLABLE | Cloud storage URL |
| created_at | TIMESTAMPTZ | NOT NULL | UTC |
| updated_at | TIMESTAMPTZ | NOT NULL | |

FK rule: user_id ON DELETE CASCADE

### 8.4 SavedAddresses

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| id | INT | PK, auto-increment | |
| customer_id | INT | FK CustomerProfiles, NOT NULL | ON DELETE CASCADE |
| label | VARCHAR(50) | NOT NULL | Home, Office, Warehouse, etc. |
| address_line_1 | VARCHAR(200) | NOT NULL | |
| address_line_2 | VARCHAR(200) | NULLABLE | |
| city | VARCHAR(100) | NOT NULL | |
| state | VARCHAR(100) | NOT NULL | |
| postal_code | VARCHAR(20) | NOT NULL | |
| latitude | DOUBLE | NULLABLE | Must be set with longitude or neither |
| longitude | DOUBLE | NULLABLE | Must be set with latitude or neither |
| is_default | BOOLEAN | NOT NULL, DEFAULT FALSE | At most one TRUE per customer (partial unique index) |
| created_at | TIMESTAMPTZ | NOT NULL | UTC |

Index: idx_saved_addresses_customer_id, uix_saved_addresses_one_default_per_customer (partial unique WHERE is_default = TRUE)

### 8.5 DriverProfiles

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| id | INT | PK, auto-increment | |
| user_id | INT | FK Users, NOT NULL, UNIQUE | UNIQUE enforces 1:1 relationship |
| phone_number | VARCHAR(20) | NULLABLE | Shown to customer after assignment |
| vehicle_type | VARCHAR(50) | NOT NULL | Bike, Van, Truck |
| vehicle_number | VARCHAR(20) | NULLABLE | Registration plate |
| license_number | VARCHAR(30) | NOT NULL, UNIQUE | |
| account_status | ENUM | NOT NULL, DEFAULT PendingApproval | PendingApproval, Active, Suspended, Deleted |
| op_status | ENUM | NULLABLE | Available, InTransit, Offline. NULL until Active |
| current_lat | DOUBLE | NULLABLE | Live GPS — Background Service writes this |
| current_lng | DOUBLE | NULLABLE | Live GPS — Background Service writes this |
| approved_by | INT | NULLABLE, FK Users | Admin who activated the account |
| approved_at | TIMESTAMPTZ | NULLABLE | Set when Admin approves. Both approved_by and approved_at set together or neither |
| created_at | TIMESTAMPTZ | NOT NULL | UTC |
| updated_at | TIMESTAMPTZ | NOT NULL | |

FK rules: user_id ON DELETE CASCADE; approved_by ON DELETE SET NULL

Indexes: uq_driver_profiles_user_id (implicit unique), uq_driver_profiles_license (implicit unique), idx_driver_profiles_account_status

### 8.6 Shipments

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| id | INT | PK, auto-increment | |
| tracking_number | VARCHAR(20) | NOT NULL, UNIQUE | Format: TRK-XXXXXX |
| customer_id | INT | FK Users, NOT NULL | Sender — always set. ON DELETE RESTRICT |
| driver_id | INT | FK Users, NULLABLE | NULL until assigned. ON DELETE SET NULL |
| status | ENUM | NOT NULL, DEFAULT Pending | See state machine |
| picked_up_at | TIMESTAMPTZ | NULLABLE | UTC — set on PickedUp. Legal POP timestamp |
| delivered_at | TIMESTAMPTZ | NULLABLE | UTC — set on Delivered. Legal POD timestamp |
| cancelled_at | TIMESTAMPTZ | NULLABLE | UTC — set on Cancelled |
| failed_at | TIMESTAMPTZ | NULLABLE | UTC — set on FailedDelivery |
| created_at | TIMESTAMPTZ | NOT NULL | UTC |
| updated_at | TIMESTAMPTZ | NOT NULL | Auto-set by trigger on update |

> **Note:** Address and recipient data are stored in the `ShipmentAddresses` child table (Section 8.7) — one Pickup row and one Dropoff row per shipment. OTP state is stored in the `ShipmentOtpWindows` child table (Section 8.8) — one Pickup row and one Delivery row per shipment. These columns do not exist on the Shipments table itself.

Indexes: uq_shipments_tracking_number (implicit unique), idx_shipments_customer_id, idx_shipments_driver_id, idx_shipments_status

### 8.7 ShipmentAddresses

Extracted child table — replaces the flat pickup/dropoff/recipient columns that would otherwise inflate the Shipments table. UNIQUE (shipment_id, address_type) enforces exactly one Pickup and one Dropoff row per shipment at the database level.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| id | INT | PK, auto-increment | |
| shipment_id | INT | FK Shipments, NOT NULL | ON DELETE CASCADE |
| address_type | ENUM | NOT NULL | Pickup, Dropoff |
| address_line | TEXT | NOT NULL | Full address string |
| lat | DOUBLE | NULLABLE | Both lat and lng set together or neither |
| lng | DOUBLE | NULLABLE | |
| contact_name | VARCHAR(100) | NULLABLE | NULL on Pickup row. Required on Dropoff row |
| contact_phone | VARCHAR(20) | NULLABLE | NULL on Pickup row. Required on Dropoff row |

UNIQUE: (shipment_id, address_type) — enforced by uq_shipment_addresses_type constraint.

FK rule: shipment_id ON DELETE CASCADE

Index: idx_shipment_addresses_shipment_id

**Design note:** The Sender is already identified via customer_id → Users on the Shipments table, so contact details are not needed on the Pickup row. The Dropoff row carries recipient contact details (contact_name, contact_phone) so the Driver can call the Recipient on arrival.

### 8.8 ShipmentOtpWindows

Extracted child table — replaces the six flat OTP columns that would otherwise live on the Shipments table. UNIQUE (shipment_id, otp_type) enforces at most one Pickup OTP row and one Delivery OTP row per shipment. Rows are inserted on first OTP request and upserted on regeneration. They are never deleted — verified_at provides a permanent audit record and otp_code is cleared (set NULL) immediately after successful verification.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| id | INT | PK, auto-increment | |
| shipment_id | INT | FK Shipments, NOT NULL | ON DELETE CASCADE |
| otp_type | ENUM | NOT NULL | Pickup (POP), Delivery (POD) |
| otp_code | CHAR(4) | NULLABLE | NULL when no active window or after verification |
| expires_at | TIMESTAMPTZ | NULLABLE | 15-minute window. NULL when no active code |
| attempt_count | SMALLINT | NOT NULL, DEFAULT 0 | Increments on wrong code. Hard cap: 3 |
| generated_at | TIMESTAMPTZ | NULLABLE | Audit: when current code was issued or last regenerated |
| verified_at | TIMESTAMPTZ | NULLABLE | Set on success. Never updated after. Permanent record |

UNIQUE: (shipment_id, otp_type) — enforced by uq_shipment_otp_type constraint.

FK rule: shipment_id ON DELETE CASCADE

Index: idx_shipment_otp_shipment_id

**Design note:** When verified_at is set, otp_code is immediately cleared (NULL) so the code cannot be re-read or replayed. The attempt_count is reset to 0 on every regeneration call.

### 8.9 ShipmentItems

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| id | INT | PK, auto-increment | |
| shipment_id | INT | FK Shipments, NOT NULL | ON DELETE CASCADE |
| description | VARCHAR(200) | NOT NULL | e.g. "Documents", "Shoe box" |
| weight_kg | DECIMAL(8,3) | NOT NULL | Per-item weight |
| length_cm | DECIMAL(6,1) | NOT NULL | |
| width_cm | DECIMAL(6,1) | NOT NULL | |
| height_cm | DECIMAL(6,1) | NOT NULL | |
| quantity | SMALLINT | NOT NULL, DEFAULT 1 | |

Computed total shipment weight = SUM(weight_kg × quantity) across all items for a shipment.

Index: idx_shipment_items_shipment_id

### 8.10 ShipmentEvents

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| id | INT | PK, auto-increment | |
| shipment_id | INT | FK Shipments, NOT NULL | ON DELETE CASCADE |
| status | ENUM | NOT NULL | Status at the moment of the event |
| description | TEXT | NOT NULL | Human-readable event message |
| latitude | DOUBLE | NULLABLE | GPS coordinate snapshot at event time |
| longitude | DOUBLE | NULLABLE | GPS coordinate snapshot at event time |
| actor_id | INT | NULLABLE, FK Users | Who triggered the event; NULL = system/BackgroundService |
| occurred_at | TIMESTAMPTZ | NOT NULL | Always ORDER BY this column ascending |

Rule: This table is append-only. Rows must never be updated or deleted in normal operation.

Index: idx_shipment_events_shipment_id

### 8.11 ShipmentStatus Enum and State Machine

Valid status values and their valid successor states:

```
Pending         -> Assigned       (Driver self-assigns via DB transaction)
Assigned        -> PickedUp       (Pickup OTP verified by Driver)
PickedUp        -> InTransit      (Driver triggers Start Transit)
InTransit       -> Arrived        (Driver triggers Arrived at Destination)
Arrived         -> Delivered      (Delivery OTP verified by Driver)
Arrived         -> FailedDelivery (Driver cannot complete delivery)
Pending         -> Cancelled      (Customer cancels before assignment)
Any non-terminal -> Cancelled     (Admin override only)
Any state        -> Any state     (Admin override only — inserts override event)
```

Terminal states (Delivered, Cancelled, FailedDelivery) — no further transitions permitted except Admin override.

Any transition not listed above shall be rejected by the state machine guard with HTTP 409 Conflict.

### 8.12 Deferred Entities

| Entity | Reason Deferred |
|--------|----------------|
| Vehicle | No fleet management FR exists. vehicle_type as a string on DriverProfile is sufficient for current scope. |
| Payment | No payment gateway FR exists. Requires its own module with webhook handling and idempotency. |
| Review / Rating | No rating FR exists. Depends on stable Delivered status. Add post-MVP. |
| RefreshTokens | Deferred — JWT-only stateless auth chosen for capstone simplicity. Add with token rotation in production version. |

---

## 9. API Contract

### 9.1 Authentication Endpoints (Public — No JWT Required)

| Method | Route | Request Body | Success Response |
|--------|-------|--------------|------------------|
| POST | /api/auth/register | email, password, fullName, role, vehicleType (Driver only), licenseNumber (Driver only) | 201 with userId, email, role |
| POST | /api/auth/login | email, password | 200 with accessToken, expiresIn |
| POST | /api/auth/logout | _(no body — token is discarded client-side)_ | 204 No Content |

> **Note:** There is no `/api/auth/refresh` endpoint. Authentication is stateless. When the access token expires, the user must call `/api/auth/login` again.

### 9.2 Customer Endpoints (Role: Customer)

| Method | Route | Description |
|--------|-------|-------------|
| POST | /api/shipments | Book new shipment with items and recipient details |
| GET | /api/shipments | Own shipments paginated with optional status filter |
| GET | /api/shipments/{id} | Full shipment detail with addresses, items, and events timeline |
| GET | /api/track/{trackingNumber} | Public tracking lookup — no auth required |

### 9.3 Driver Endpoints (Role: Driver — Active Account Required)

| Method | Route | Description |
|--------|-------|-------------|
| GET | /api/drivers/me | Own driver profile |
| PUT | /api/drivers/op-status | Toggle operational status |
| GET | /api/shipments/queue | Pending shipments available to claim |
| PUT | /api/shipments/{id}/assign | Self-assign a pending shipment |
| POST | /api/shipments/{id}/request-pickup-otp | Generate and push Pickup OTP to Sender |
| POST | /api/shipments/{id}/verify-pickup-otp | Submit Pickup OTP — transitions to PickedUp |
| PUT | /api/shipments/{id}/status | Transition: PickedUp to InTransit, or InTransit to Arrived |
| POST | /api/shipments/{id}/request-delivery-otp | Generate and push Delivery OTP to Recipient |
| POST | /api/shipments/{id}/verify-delivery-otp | Submit Delivery OTP — transitions to Delivered |
| POST | /api/shipments/{id}/regenerate-otp | Regenerate expired Pickup or Delivery OTP |

### 9.4 Admin Endpoints (Role: Admin)

| Method | Route | Description |
|--------|-------|-------------|
| GET | /api/admin/drivers | All drivers with account_status filter |
| GET | /api/admin/drivers/{id} | Driver detail with shipment history |
| PUT | /api/admin/drivers/{id}/status | Update driver account status |
| GET | /api/admin/shipments | All shipments with filters |
| GET | /api/admin/shipments/{id} | Full shipment detail |
| PUT | /api/admin/shipments/{id}/override-status | Force-set any shipment status |
| GET | /api/admin/dashboard | Platform-wide metrics and counts |

---

## 10. System Workflows

### 10.1 Complete End-to-End Delivery Workflow

**Step 1 — Booking**
Customer submits booking form. API creates Shipment + ShipmentAddresses (Pickup row + Dropoff row) + ShipmentItems. TrackingNumber generated. Status = Pending. ShipmentEvent inserted: "Shipment booked".

**Step 2 — Driver Assignment**
Driver views pending queue. Driver calls assign endpoint. EF Core transaction begins. Shipment row read with row lock. Status verified as Pending. driver_id set. Status = Assigned. Transaction committed. ShipmentEvent inserted: "Driver assigned". Second concurrent assign attempt returns 409.

**Step 3 — Pickup OTP**
Driver calls request-pickup-otp. Backend generates 4-digit OTP. Stored in ShipmentOtpWindows (otp_type=Pickup) with expires_at = UTC now + 15 minutes. SignalR pushes PickupOtpGenerated event to Sender's connection only. Sender's screen shows OTP and countdown. Sender reads OTP aloud to Driver. Driver enters OTP in Driver UI. Backend validates: match, not expired, attempt_count under 3. Status = PickedUp. picked_up_at = UTC now. otp_code cleared (NULL), verified_at set. ShipmentEvent: "Parcel collected from sender — POP confirmed". SignalR broadcasts StatusUpdated to group.

**Step 4 — Start Transit**
Driver calls update-status with InTransit. Status = InTransit. Driver op_status = InTransit. ShipmentEvent inserted with current coordinates. SignalR broadcasts StatusUpdated to group.

**Step 5 — Live Tracking**
Background Service fires every 5 seconds. Queries all InTransit shipments. For each: interpolates Driver's coordinates toward dropoff using the ShipmentAddresses Dropoff row coordinates. Updates DriverProfile.current_lat and current_lng. Calls IHubContext to broadcast LocationUpdated to shipment's SignalR group. Sender and Recipient tracking screens update map pin in real time without page refresh.

**Step 6 — Arrived**
Driver calls update-status with Arrived. Status = Arrived. ShipmentEvent with coordinates. SignalR broadcasts DriverArrived and StatusUpdated to group.

**Step 7 — Delivery OTP**
Driver calls request-delivery-otp. Backend generates new 4-digit OTP. Stored in ShipmentOtpWindows (otp_type=Delivery) with expires_at = UTC now + 15 minutes. SignalR pushes DeliveryOtpGenerated event to Recipient's connection only. Recipient's screen shows OTP and countdown. Recipient reads OTP aloud to Driver. Driver enters OTP in Driver UI. Backend validates: match, not expired, attempt_count under 3. Status = Delivered. delivered_at = UTC now. otp_code cleared (NULL), verified_at set. Driver op_status = Available. ShipmentEvent: "Delivered to recipient — POD confirmed". SignalR broadcasts ShipmentDelivered to group. Both Sender and Recipient screens show success state. SignalR clients leave group.

### 10.2 OTP Failure and Regeneration Workflow

Driver submits wrong OTP. attempt_count on ShipmentOtpWindows row increments. API returns 400 with "Incorrect OTP. N attempts remaining." At third failure, API returns 429: "Maximum attempts reached. Please regenerate the OTP." Driver calls regenerate-otp. New 4-digit code generated. expires_at reset to now + 15 minutes. attempt_count reset to 0. New OTP pushed to Sender (pickup) or Recipient (delivery) via SignalR. OtpRegenerated event broadcast to group (without the OTP value).

---

## 11. SignalR Real-Time Event Contract

### 11.1 Hub: TrackingHub

Group naming convention: shipment-{trackingNumber} (example: shipment-TRK-A3X9B1)

Angular clients join the group when opening the tracking page or driver job screen. Angular clients leave the group when ShipmentDelivered is received or the user navigates away.

### 11.2 Server to Client Events

| Event Name | Trigger | Payload | Recipient |
|------------|---------|---------|-----------|
| LocationUpdated | Background Service GPS tick | trackingNumber, latitude, longitude, timestamp | All group members |
| StatusUpdated | Any status transition | trackingNumber, newStatus, description, timestamp | All group members |
| PickupOtpGenerated | Driver requests Pickup OTP | trackingNumber, otpCode, expiresAt | Sender connection only |
| DriverArrived | Driver triggers Arrived | trackingNumber, timestamp, driverLat, driverLng | All group members |
| DeliveryOtpGenerated | Driver requests Delivery OTP | trackingNumber, otpCode, expiresAt | Recipient connection only |
| ShipmentDelivered | Delivery OTP verified | trackingNumber, deliveredAt | All group members |
| OtpRegenerated | Regenerate endpoint called | trackingNumber, otpType, expiresAt | Sender or Recipient only (no OTP value) |

Security note on OTP targeting: PickupOtpGenerated is pushed only to the Sender's SignalR connection ID, not broadcast to the entire group. Same for DeliveryOtpGenerated to the Recipient. This prevents the Driver or any observer from intercepting the OTP via the shared group channel.

### 11.3 Client to Server Hub Methods

| Hub Method | Called By | Purpose |
|------------|-----------|---------|
| JoinShipmentGroup(trackingNumber) | Customer or Driver | Subscribe to all shipment events |
| LeaveShipmentGroup(trackingNumber) | Any | Unsubscribe on delivery or page navigation |

---

## 12. State Machine

### 12.1 Valid Transitions

| From | To | Triggered By | Guard Condition |
|------|----|--------------|-----------------|
| — | Pending | Customer booking | Successful shipment creation |
| Pending | Assigned | Driver self-assign | Driver Active + Available; DB transaction with lock |
| Assigned | PickedUp | Driver verify-pickup-otp | Pickup OTP valid, not expired, attempts under 3 |
| PickedUp | InTransit | Driver update-status | Driver owns this shipment in PickedUp state |
| InTransit | Arrived | Driver update-status | Driver owns this shipment in InTransit state |
| Arrived | Delivered | Driver verify-delivery-otp | Delivery OTP valid, not expired, attempts under 3 |
| Arrived | FailedDelivery | Driver update-status | Driver reports failed delivery |
| Pending | Cancelled | Customer | Status must be Pending; customer owns the shipment |
| Any non-terminal | Cancelled | Admin override | Admin role required; creates override ShipmentEvent |
| Any | Any | Admin override | Admin role required; always inserts ShipmentEvent |

### 12.2 Invalid Transitions — HTTP 409 Returned

Any transition not listed above is rejected. Representative invalid examples: Pending to Delivered (skipping all stages), Assigned to InTransit (skipping PickedUp and POP verification), Delivered to any state (terminal state), PickedUp back to Assigned (reverse transition).

---

## 13. Implementation Roadmap

### Phase 0 — Project Scaffolding (Day 1)
Apply DB schema (SwiftParcel_Schema_v4.1.sql) to PostgreSQL. Scaffold EF Core DbContext and entity models using `dotnet ef dbcontext scaffold`. Initialise .NET 8 Web API with folder structure: Controllers, Services, Repositories, Models, DTOs, Hubs, BackgroundServices, Middleware, Enums. Initialise Angular project with feature modules: auth, customer, driver, admin, shared. Configure AppDbContext with all entity DbSets. Configure global exception handler middleware. Configure CORS policy for localhost:4200.

Checkpoint: API boots and returns 200 on health endpoint. Angular loads blank modules. EF Core can query the seeded admin user.

### Phase 1 — Authentication and JWT (Days 2–3)
Implement AuthService: register (Customer and Driver paths), login with JWT issuance, logout (returns 204 — no DB write needed). Configure JWT Bearer middleware with signing key from appsettings. Store JWT signing secret in environment variable or user secrets — never hardcoded. Build AuthController. Build Angular AuthModule with register and login forms. Implement AuthGuard and AuthInterceptor in Angular. Store access token in Angular service memory only — cleared on logout or page refresh.

Checkpoint: Register Customer and Driver, login both, inspect JWT claims in jwt.io, confirm 401 on protected endpoint without token, confirm 403 on cross-role access.

### Phase 2 — Shipment Booking (Days 4–5)
Implement TrackingNumber generator. Implement ShipmentService: create (inserts Shipment + ShipmentAddresses Pickup/Dropoff rows + ShipmentItems in one transaction), list paginated, get by ID, public tracking lookup. Build ShipmentController. Build Angular CustomerModule with booking form, shipment list, and static tracking detail page.

Checkpoint: Book shipment, retrieve paginated list, look up by TrackingNumber. Verify ShipmentAddresses has exactly 2 rows per shipment.

### Phase 3 — Driver Management (Days 6–7)
Implement DriverService: profile read, op-status toggle, pending queue listing. Implement self-assign with EF Core transaction and row lock. Implement state machine guard in ShipmentService. Build DriverController and AdminController. Build Angular DriverModule: pending queue and self-assign. Build Angular AdminModule: driver list and approve/suspend actions.

Checkpoint: Driver registers, Admin approves, Driver toggles Available, Driver assigns shipment, confirm 409 on double-assign.

### Phase 4 — OTP System (Days 8–9)
Implement cryptographic OTP generator. Implement ShipmentService methods: request-pickup-otp (upsert ShipmentOtpWindows Pickup row), verify-pickup-otp, request-delivery-otp (upsert ShipmentOtpWindows Delivery row), verify-delivery-otp, regenerate-otp. Wire all OTP endpoints. Build Angular Driver UI: OTP input prompts for both pickup and delivery. Build Angular Customer/Recipient UI: OTP display cards with countdown timers.

Checkpoint: Full dual-OTP flow. Test expiry, wrong code, 3-attempt lockout, and regeneration for both pickup and delivery. Verify otp_code is NULL and verified_at is set after successful verification.

### Phase 5 — SignalR Real-Time (Days 10–11)
Define TrackingHub with JoinShipmentGroup and LeaveShipmentGroup. Implement GPS Background Service: query InTransit shipments, join ShipmentAddresses Dropoff row for target coordinates, interpolate Driver coordinates, push LocationUpdated via IHubContext. Wire OTP generation methods to push targeted events to Sender or Recipient connection only. Wire all status transitions to broadcast StatusUpdated, DriverArrived, and ShipmentDelivered. Build Angular SignalRService wrapping the @microsoft/signalr HubConnection. Connect tracking page to SignalR. Implement reconnection logic.

Checkpoint: Full live tracking test — assign, pickup OTP, transit, GPS pin moves, arrive, delivery OTP, delivered success screen.

### Phase 6 — Admin and Polish (Days 12–13)
Build Admin dashboard endpoint and Angular dashboard page. Implement admin override endpoint. Add DTO validation with Data Annotations on all endpoints. Add Authorize role attributes to all routes. Add ILogger logging at all significant events with no OTP values. Implement booking idempotency check. Add Angular loading states, error toasts, and empty state screens. Review EF Core queries for N+1 issues.

Checkpoint: Role isolation (Customer hits Driver route returns 403), OTP absent from logs, booking idempotency works.

### Phase 7 — Testing and Documentation (Day 14)
Unit tests: ShipmentService (status transitions, dual OTP logic, idempotency). Unit tests: AuthService (password hashing, JWT generation). Integration tests: booking flow, self-assign race condition, full dual-OTP lifecycle. Swagger auto-documentation via Swashbuckle at /swagger in development. Final SRS review against implementation.

Checkpoint: All tests pass. Swagger shows all endpoints with correct auth and role annotations.

---

## 14. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Race condition on driver self-assign | Medium | High | EF Core transaction with SELECT FOR UPDATE equivalent; second claim returns 409 |
| OTP brute-force (4-digit = 10,000 combinations) | Low | High | Max 3 attempts enforced in DB via attempt_count on ShipmentOtpWindows; lockout requires regeneration |
| OTP pushed to wrong SignalR connection | Low | High | Target by Connection ID not group broadcast; verify user identity before push |
| SignalR connection drops mid-delivery | Medium | Medium | Angular reconnection with exponential backoff; client re-joins group on reconnect |
| Background Service blocks main thread | Low | Medium | Use await Task.Delay not Thread.Sleep; dedicated background thread |
| JWT stored in localStorage exposing to XSS | Medium | High | Store access token in Angular memory service only — no localStorage, no cookie. Token is lost on page refresh; user must re-login. Acceptable tradeoff for capstone stateless auth |
| EF Core migration conflicts in team development | Medium | Medium | Schema is DB-first — never edit the SQL schema and re-scaffold without team coordination; always re-scaffold together |
| Driver suspended while mid-delivery | Low | Medium | Service layer warns Admin on suspension; Admin makes the decision |
| Duplicate TrackingNumber generation | Very Low | High | Validate uniqueness after generation; retry with new random string on collision |
| Admin override creates audit gap | Low | Medium | Every override inserts a ShipmentEvent with "Admin override" description and actor_id |
| Token expiry disrupts active user session | Medium | Low | Acceptable for capstone — user re-logs in. Mitigated by setting a reasonable expiry (15–60 min configurable) |

---

*End of Document*
*SRS Version 2.1 and Requirements Understanding Document*
*B2C Peer-to-Peer Courier Service Platform — modelled on Rapido Parcel delivery*
