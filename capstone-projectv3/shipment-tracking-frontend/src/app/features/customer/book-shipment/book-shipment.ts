import { Component, computed, signal, AfterViewInit } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { BookShipmentRequest } from '../../../core/models/shipment-models/BookShipment.model';
import { ShipmentService } from '../../../core/services/shipment/shipment.service';

@Component({
  selector: 'app-book-shipment',
  imports: [RouterModule, MatIconModule, FormsModule],
  templateUrl: './book-shipment.html',
  styleUrl: './book-shipment.css',
})
export class BookShipment implements AfterViewInit {
  currentStep = signal(1);
  editSection: string | null = null;

  shipmentData = signal<BookShipmentRequest>({
    pickupAddress: '',
    pickupLat: null,
    pickupLng: null,
    dropoffAddress: '',
    dropoffLat: null,
    dropoffLng: null,
    recipientName: '',
    recipientPhone: '',
    items: [
      {
        description: '',
        weight: 0,
        quantity: 1,
        height: null,
        width: null,
        length: null,
      },
    ],
  });

  totalItems = computed(() => this.shipmentData().items.length);

  totalQuantity = computed(() =>
    this.shipmentData().items.reduce((sum, item) => sum + item.quantity, 0),
  );

  totalWeight = computed(() =>
    this.shipmentData().items.reduce((sum, item) => sum + item.weight * item.quantity, 0),
  );

  constructor(
    private router: Router,
    private shipmentService: ShipmentService,
  ) {
    this.shipmentService = shipmentService;
    const previousShipment = history.state.shipment;
    const editSection = history.state.editSection;

    if (previousShipment) {
      this.shipmentData.set(previousShipment);
    }

    if (editSection) {
      this.editSection = editSection;
    }
  }

  ngAfterViewInit() {
    if (this.editSection) {
      const section = document.getElementById(this.editSection);
      section?.scrollIntoView({
        behavior: 'smooth',
        block: 'start',
      });
    }
  }

  addItem() {
    this.shipmentData.update((data) => ({
      ...data,
      items: [
        ...data.items,
        {
          description: '',
          weight: 0,
          quantity: 1,
          height: null,
          width: null,
          length: null,
        },
      ],
    }));
  }

  removeItem(index: number) {
    this.shipmentData.update((data) => ({
      ...data,
      items: data.items.filter((_, i) => i !== index),
    }));
  }

  goToReview() {
    this.shipmentService.getShipmentQuoteApi(this.shipmentData()).subscribe({
      next: (quote) => {
        console.log('Shipment Quote:', quote);

        this.router.navigate(['/customer/review-shipment'], {
          state: {
            shipment: this.shipmentData(),
            quote: quote,
          },
        });
      },
      error: (error) => {
        console.error('Quote Error:', error);
      },
    });
  }
}
