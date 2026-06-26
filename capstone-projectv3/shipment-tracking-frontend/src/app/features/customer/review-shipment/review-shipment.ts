import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { ShipmentService } from '../../../core/services/shipment/shipment.service';
import { BookShipmentRequest } from '../../../core/models/shipment-models/BookShipment.model';
import { ShipmentQuoteResponse } from '../../../core/models/shipment-models/ShipmentQuoteResponse.model';
import { MatIcon, MatIconModule } from "@angular/material/icon";

@Component({
  selector: 'app-review-shipment',
  imports: [MatIconModule],
  templateUrl: './review-shipment.html',
  styleUrl: './review-shipment.css'
})
export class ReviewShipment {
  shipment!: BookShipmentRequest;
  quote!: ShipmentQuoteResponse;

  constructor(
    private router: Router,
    private shipmentService: ShipmentService
  ) {
    this.shipment = history.state.shipment;
    this.quote=history.state.quote;
  }

  // Navigate back to specific section
  editSection(section: string) {
    this.router.navigate(['/customer/book-shipment'], {
      state: {
        shipment: this.shipment,
        editSection: section
      }
    });
  }

  // Total items
  get totalItems(): number {
    return this.shipment.items.length;
  }

  // Total quantity
  get totalQuantity(): number {
    return this.shipment.items.reduce(
      (sum, item) => sum + item.quantity,
      0
    );
  }

  // Total weight
  get totalWeight(): string {
    const total = this.shipment.items.reduce(
      (sum, item) => sum + (item.weight * item.quantity),
      0
    );

    return total.toFixed(2);
  }

  // Per item total weight
  getItemTotalWeight(weight: number, quantity: number): string {
    return (weight * quantity).toFixed(2);
  }

  // Confirm shipment
  confirmShipment() {
    this.shipmentService.bookShipmentApi(this.shipment).subscribe({
      next: (response) => {
        console.log('Shipment Created:', response);

        this.router.navigate(['/customer/confirm-shipment'], {
          state: {
            shipment: this.shipment,
            trackingNumber: response.trackingNumber,
            status: response.status,
            shipmentId: response.id
          }
        });
      },
      error: (error) => {
        console.error('Booking Error:', error);
      }
    });
  }
}