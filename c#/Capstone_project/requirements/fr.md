Here is the complete, updated Software Requirements Specification (SRS) for your B2C Logistics & Shipment Tracking System.

Having a formalized document like this is a massive asset when discussing your projects during campus placements or technical assessments. It proves you can translate raw business ideas into concrete engineering tasks.

---

## 1. Project Overview

A layered, enterprise-grade B2C web application that facilitates end-to-end package delivery. The system allows customers to book shipments, drivers to accept and fulfill these requests, and admins to oversee the operation. Key features include real-time GPS route simulation via WebSockets and a Secure Proof of Delivery (POD) system utilizing One-Time Passwords (OTP).

## 2. Technical Architecture & Stack

Running a complex, multi-layered stack can sometimes strain local development environments. To keep things running smoothly on a system with 8GB of RAM, running the Angular frontend, .NET API, and a local PostgreSQL instance natively (rather than using heavy Docker containers) is the recommended approach for this architecture.

| Component | Technology | Purpose |
| --- | --- | --- |
| **Frontend** | Angular, TypeScript | Client-facing UI for Customers, Drivers, and Admins. |
| **Backend API** | ASP.NET Core Web API (C#) | RESTful API using Controllers, Services, and Repositories. |
| **Database** | PostgreSQL, EF Core | Relational data storage utilizing Code-First Migrations. |
| **Real-Time** | SignalR | WebSocket connections for live tracking updates. |
| **Security** | JWT (JSON Web Tokens) | Role-Based Access Control (RBAC) and authentication. |

---

## 3. User Roles

* **Customer (Sender):** Registers accounts, books shipments, pays/confirms, and tracks packages live.
* **Driver (Courier):** Manages availability, accepts delivery requests, drives the simulated route, and validates delivery via OTP.
* **Admin (Dispatcher):** Oversees platform health, approves driver registrations, and handles data overrides.

---

## 4. Functional Requirements

### Module 1: Shipment Booking

* **FR1.1:** The system shall allow authenticated Customers to submit a new shipment form containing pickup address, drop-off address, weight, and dimensions.
* **FR1.2:** The API shall generate a unique, alphanumeric `TrackingNumber` upon successful booking.
* **FR1.3:** The backend shall set the initial status of a newly created shipment to `Pending`.
* **FR1.4:** Customers shall be able to retrieve a paginated history of all their past and active shipments.

### Module 2: Driver Management

* **FR2.1:** Drivers shall be able to register their profile, which defaults to a `Pending Approval` state in the database.
* **FR2.2:** Admins shall have the ability to read all driver data and update a Driver's status to `Active`, `Suspended`, or `Deleted`.
* **FR2.3:** Active Drivers shall be able to toggle their daily operational status (`Available`, `In Transit`, `Offline`).
* **FR2.4:** The system shall allow Drivers to view a queue of `Pending` shipments and assign a specific shipment to their account.

### Module 3: Tracking & Push Updates

* **FR3.1:** The system shall utilize an ASP.NET Core Background Service to simulate GPS movement by incrementally updating coordinates for shipments marked as `In Transit`.
* **FR3.2:** The backend shall utilize SignalR to push these simulated coordinates directly to the Angular frontend.
* **FR3.3:** Customers shall be able to query a tracking endpoint using their `TrackingNumber` to view current status and a chronological event timeline.
* **FR3.4:** The Angular frontend shall listen to the SignalR hub and visually update a map pin or progress bar without requiring a page refresh.

### Module 4: Delivery Confirmation & Secure POD

* **FR4.1:** Drivers shall be able to trigger an "Arrived" status update when they reach the destination.
* **FR4.2:** Upon the "Arrived" trigger, the backend shall generate a 4-digit OTP, save it to the shipment record with a 15-minute expiration, and broadcast it to the Customer's live tracking screen.
* **FR4.3:** The Driver UI shall prompt the driver to input the OTP provided by the Customer.
* **FR4.4:** The backend shall verify the submitted OTP against the database record; if successful, it will update the status to `Delivered` and record the exact UTC timestamp.
* **FR4.5:** The backend shall broadcast a final SignalR event to terminate the tracking session and display a success state on the Customer's screen.

---

## 5. Non-Functional Requirements (NFRs)

* **Security (NFR1):** All API endpoints except the public tracking lookup must be secured using JWT authentication, with strict role claims validation.
* **Real-Time Performance (NFR2):** SignalR push updates from the backend to the frontend must execute with minimal latency to ensure a smooth simulation.
* **Data Integrity (NFR3):** Entity Framework Core must utilize transactions to prevent race conditions, ensuring two drivers cannot simultaneously accept the same pending shipment.
* **Error Handling (NFR4):** The API must implement a global exception handler middleware to return standardized HTTP response codes (404 Not Found, 403 Forbidden, 400 Bad Request) instead of exposing server stack traces.

---

With the master blueprint finished, what specific part of the implementation would you like to tackle first?