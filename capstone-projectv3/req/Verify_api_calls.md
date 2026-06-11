Good, your API looks complete. Here's the exact test sequence — copy-paste ready.

---

## Step 1 — Admin approves the Driver first
*(do this before anything else)*

**Login as Admin**
```
POST /api/Auth/login
{
  "email": "admin@swiftparcel.com",
  "password": "your-admin-password"
}
```
Copy the `accessToken` → paste in Swagger padlock as `Bearer <token>`

**Get all drivers to find the driver's id**
```
GET /api/Admin/drivers
```

**Approve the Driver**
```
PUT /api/Admin/drivers/status
{
  "userId": <driverUserId>,
  "accountStatus": "Active"
}
```

---

## Step 2 — Customer books a shipment

**Login as Customer**
```
POST /api/Auth/login
{
  "email": "customer@test.com",
  "password": "your-password"
}
```
Switch Swagger padlock to Customer JWT.

**Book a shipment**
```
POST /api/shipments
{
  "pickupAddress": {
    "addressLine": "123 Sender Street, Hyderabad",
    "lat": 17.3850,
    "lng": 78.4867
  },
  "dropoffAddress": {
    "addressLine": "456 Recipient Road, Hyderabad",
    "lat": 17.4399,
    "lng": 78.4983,
    "contactName": "John Doe",
    "contactPhone": "9876543210"
  },
  "items": [
    {
      "description": "Documents",
      "weightKg": 0.5,
      "lengthCm": 30,
      "widthCm": 20,
      "heightCm": 5,
      "quantity": 1
    }
  ]
}
```
Copy the `trackingNumber` (e.g. `TRK-A3X9B1`) and the `shipmentId` from the response.

**Verify booking**
```
GET /api/shipments/{id}
GET /api/track/TRK-A3X9B1    ← public, no JWT needed
```

---

## Step 3 — Driver sets Available and self-assigns

Switch Swagger padlock to Driver JWT.

```
POST /api/Auth/login   ← login as driver, get token
```

**Set Driver available**
```
PUT /api/Driver/status
{
  "opStatus": "Available"
}
```

**View the job queue — shipment should appear**
```
GET /api/shipments/queue
```

**Self-assign the shipment**
```
PUT /api/shipments/{id}/assign
```
Response should show `status: "Assigned"`.

**Verify on tracking**
```
GET /api/track/TRK-A3X9B1   ← status should now be Assigned
```

---

## Step 4 — Pickup OTP flow

*(Still Driver JWT in Swagger, open Postman with Customer JWT connected to hub)*

**Request Pickup OTP**
```
POST /api/shipments/{id}/request-pickup-otp
```

→ In Postman hub you should see `PickupOtpGenerated` arrive with a 4-digit code.
→ If Postman isn't connected yet, the OTP is silently skipped — check the DB directly:
```sql
SELECT otp_code, expires_at, attempt_count 
FROM shipment_otp_windows 
WHERE shipment_id = {id} AND otp_type = 'Pickup';
```

**Verify Pickup OTP** *(paste code from Postman or DB)*
```
POST /api/shipments/{id}/verify-pickup-otp
{
  "otpCode": "4827"
}
```
Response should show `status: "PickedUp"`, `success: true`.

**Test wrong OTP first (optional)**
```
POST /api/shipments/{id}/verify-pickup-otp
{
  "otpCode": "0000"
}
→ 400: "Incorrect OTP. 2 attempts remaining."

POST again with wrong code
→ 400: "Incorrect OTP. 1 attempt remaining."

POST again with wrong code
→ 429: "Maximum attempts reached. Please regenerate the OTP."

POST /api/shipments/{id}/regenerate-otp
{
  "otpType": "Pickup"
}
→ New code pushed to Postman, attempt count reset to 0
```

---

## Step 5 — Transit and Arrival

**Start transit**
```
PUT /api/shipments/{id}/status
{
  "status": "InTransit"
}
```
→ In Postman hub watch `LocationUpdated` events arrive every 5 seconds automatically (GPS simulation ticking).

**Trigger Arrived**
```
PUT /api/shipments/{id}/status
{
  "status": "Arrived"
}
```
→ Postman hub: `DriverArrived` event arrives.

---

## Step 6 — Delivery OTP flow

**Request Delivery OTP**
```
POST /api/shipments/{id}/request-delivery-otp
```
→ Postman hub: `DeliveryOtpGenerated` arrives with a new 4-digit code.

**Verify Delivery OTP**
```
POST /api/shipments/{id}/verify-delivery-otp
{
  "otpCode": "5391"
}
```
→ Postman hub: `ShipmentDelivered` broadcast arrives.
→ Response shows `status: "Delivered"`, `success: true`.

---

## Step 7 — Confirm final state

```
GET /api/track/TRK-A3X9B1
```
Should show:
```json
{
  "status": "Delivered",
  "deliveredAt": "2026-06-08T...",
  "events": [
    { "status": "Pending",    "description": "Shipment booked" },
    { "status": "Assigned",   "description": "Driver assigned" },
    { "status": "PickedUp",   "description": "Parcel collected — POP confirmed" },
    { "status": "InTransit",  "description": "Driver is on the way" },
    { "status": "Arrived",    "description": "Driver arrived at destination" },
    { "status": "Delivered",  "description": "Parcel delivered — POD confirmed" }
  ]
}
```

---

## Quick reference — what to check at each stage

| After this call | Check this |
|---|---|
| Book shipment | `GET /api/track/{trackingNumber}` → status = Pending |
| Driver assigns | `GET /api/track/{trackingNumber}` → status = Assigned |
| Pickup OTP requested | Postman hub → `PickupOtpGenerated` received OR check DB `otp_code` column |
| Pickup OTP verified | Response `success: true`, DB `verified_at` set, `otp_code` = NULL |
| InTransit | Postman hub → `LocationUpdated` every 5s, DB `current_lat/lng` changing |
| Arrived | Postman hub → `DriverArrived` received |
| Delivery OTP verified | Postman hub → `ShipmentDelivered`, DB `delivered_at` set |