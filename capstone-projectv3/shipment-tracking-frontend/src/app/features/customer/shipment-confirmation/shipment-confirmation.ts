import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-shipment-confirmation',
  imports: [MatIconModule],
  templateUrl: './shipment-confirmation.html',
  styleUrl: './shipment-confirmation.css',
})
export class ShipmentConfirmation {
    trackingNumber = 'FS9876543210';

  shipment = {
    pickupAddress: '123 Tech Park, Madhapur, Hyderabad',
    dropoffAddress: '456 Office Tower, Koramangala, Bengaluru',
    recipientName: 'Rahul Sharma',
    recipientPhone: '+919876543210',
    estimatedDelivery: 'May 28, 2024 by 03:00 PM',
    totalWeight: '3.0 kg',
    totalQuantity: 3
  };

  copyTrackingNumber() {
    navigator.clipboard.writeText(this.trackingNumber);
  }
}


