Here's the exact step-by-step for Postman SignalR connection:

---

## Step 1 — Get your JWT first

Login in Swagger, copy the `accessToken`. You'll need it shortly.

---

## Step 2 — Create a new SignalR request in Postman

```
Click New → Select "SignalR Request"
```

If you don't see SignalR in the list, your Postman is below v10. Update it.

---

## Step 3 — Set the URL with JWT as query param

```
http://localhost:{port}/hubs/tracking?access_token=YOUR_JWT_HERE
```

Example:
```
http://localhost:5000/hubs/tracking?access_token=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Why query param and not header?** — WebSocket protocol doesn't support custom headers during the handshake. SignalR is designed to receive the JWT this way, and the `OnMessageReceived` event in your `Program.cs` handles it.

---

## Step 4 — Configure the connection

In Postman's SignalR tab set these:

```
Protocol Version : SignalR (not plain WebSocket)
```

Under Headers add:
```
Key   : Content-Type
Value : application/json
```

---

## Step 5 — Connect

```
Click "Connect"
```

You should see in the Postman messages panel:
```
Connected
{"type":6}   ← this is SignalR's keep-alive ping, means it's working
```

If you see a 401 error, your JWT is wrong or expired — re-login and get a fresh token.

If you see a 404, the hub is not mapped — check `app.MapHub<TrackingHub>("/hubs/tracking")` is in your `Program.cs`.

---

## Step 6 — Join the shipment group

In Postman's **Messages** section at the bottom:

```
Select "Send" tab
Target   : JoinShipmentGroup
Arguments: ["TRK-A3X9B1"]    ← your actual tracking number
```

Click **Send**.

You should immediately see a response in the messages panel:
```json
{
  "trackingNumber": "TRK-A3X9B1",
  "newStatus": "Connected",
  "description": "Joined tracking group. Live updates active.",
  "timestamp": "2026-06-08T..."
}
```

That's the `StatusUpdated` ack from `TrackingHub.JoinShipmentGroup`.

---

## Step 7 — Listen for events

Now go to Swagger and trigger actions. Events will appear in Postman's messages panel automatically:

```
Swagger: PUT /api/shipments/{id}/assign
→ Postman shows:
  StatusUpdated { newStatus: "Assigned", description: "Driver assigned..." }

Swagger: POST /api/shipments/{id}/request-pickup-otp
→ Postman shows:
  PickupOtpGenerated { otpCode: "4827", expiresAt: "..." }
  
Swagger: PUT /api/shipments/{id}/status → InTransit
→ Postman shows every 5 seconds:
  LocationUpdated { latitude: 17.3912, longitude: 78.4901, timestamp: "..." }
  LocationUpdated { latitude: 17.3974, longitude: 78.4935, timestamp: "..." }
  ...

Swagger: PUT /api/shipments/{id}/status → Arrived
→ Postman shows:
  DriverArrived { driverLat: ..., driverLng: ..., timestamp: "..." }

Swagger: POST /api/shipments/{id}/request-delivery-otp
→ Postman shows:
  DeliveryOtpGenerated { otpCode: "5391", expiresAt: "..." }

Swagger: POST /api/shipments/{id}/verify-delivery-otp
→ Postman shows:
  ShipmentDelivered { deliveredAt: "..." }
```

---

## If LocationUpdated is not showing

That means `GpsSimulationService` is not finding the shipment. Check:

```sql
-- Should return a row with non-null lat/lng on the Dropoff row
SELECT sa.lat, sa.lng 
FROM shipment_addresses sa
WHERE sa.shipment_id = {id} 
AND sa.address_type = 'Dropoff';

-- Driver should have starting coordinates
SELECT current_lat, current_lng 
FROM driver_profiles 
WHERE user_id = {driverUserId};
```

If `current_lat` is NULL on the driver, the GPS service skips that shipment. Set a starting position manually for testing:

```sql
UPDATE driver_profiles 
SET current_lat = 17.3850, current_lng = 78.4867
WHERE user_id = {driverUserId};
```

Then watch Postman — `LocationUpdated` should start arriving within 5 seconds.