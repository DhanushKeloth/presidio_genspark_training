import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { Booking } from '../../models/booking.models';
import { BookingService } from '../../services/booking.service';

@Component({
  selector: 'app-payment',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="min-h-screen bg-slate-100 px-4 py-6" *ngIf="bus as activeBus">
      <div class="mx-auto max-w-7xl">
        <div class="mb-3 flex items-center justify-between rounded-t-xl bg-slate-900 px-4 py-3 text-sm text-white">
          <p class="font-semibold">BusBooker</p>
          <p>100% Secure Payment</p>
        </div>

        <section class="rounded-b-xl border border-slate-200 bg-white p-6 shadow-sm">
          <button class="mb-3 text-sm text-slate-600" (click)="goBackToPassenger()">← Back to Seats</button>
          <h1 class="text-4xl font-bold text-slate-900">Review Your Booking</h1>
          <p class="mt-1 text-sm text-slate-600">Please review your booking details before making the payment.</p>

            <div class="mt-4 grid gap-4 lg:grid-cols-[1.2fr_0.9fr]">
              <div class="space-y-4">
                <div class="rounded-xl border border-slate-200 p-4">
                  <p class="mb-3 font-semibold text-slate-900">Trip Summary</p>
                  <div class="flex items-start justify-between gap-4">
                    <div>
                      <h2 class="text-2xl font-semibold text-slate-900">{{ activeBus.name }}</h2>
                      <p class="text-sm text-slate-500">{{ activeBus.type }}</p>
                    </div>
                    <div class="h-20 w-32 rounded-lg bg-slate-200"></div>
                  </div>

                  <div class="mt-4 grid grid-cols-3 items-center text-center">
                    <div class="text-left">
                      <p class="text-xl font-semibold text-slate-900">{{ activeBus.from }}</p>
                      <p class="text-sm font-medium text-blue-700">{{ activeBus.departureTime }}</p>
                    </div>
                    <div>
                      <p class="text-sm text-slate-500">{{ activeBus.duration }}</p>
                      <p class="text-xs text-slate-500">→</p>
                    </div>
                    <div class="text-right">
                      <p class="text-xl font-semibold text-slate-900">{{ activeBus.to }}</p>
                      <p class="text-sm font-medium text-blue-700">{{ activeBus.arrivalTime }}</p>
                    </div>
                  </div>

                  <div class="mt-3 flex flex-wrap gap-6 text-sm text-slate-600">
                    <p>{{ activeBus.date }}</p>
                    <p>Seats: {{ seats.join(', ') }}</p>
                  </div>
                </div>

                <div class="rounded-xl border border-slate-200 p-4">
                  <p class="mb-2 font-semibold text-slate-900">Passenger Details</p>
                  <div class="space-y-3">
                    <div *ngFor="let passenger of passengers; index as i" class="border-b border-slate-100 pb-2 last:border-none">
                      <p class="font-medium text-slate-900">{{ i + 1 }}. {{ passenger.name || 'Passenger' }}</p>
                      <p class="text-sm text-slate-600">Age: {{ passenger.age || '-' }} &nbsp; • &nbsp; {{ passenger.gender || '-' }} &nbsp; • &nbsp; Seat: {{ passenger.seatNumber }}</p>
                    </div>
                  </div>
                </div>
              </div>

              <div class="space-y-4">
                <div class="rounded-xl border border-slate-200 p-4">
                  <p class="mb-3 font-semibold text-slate-900">Fare Details</p>
                  <div class="space-y-2 text-sm text-slate-700">
                    <div class="flex justify-between"><span>Seat Price (₹{{ activeBus.price }} x {{ seats.length }})</span><span>₹ {{ activeBus.price * seats.length }}</span></div>
                    <div class="flex justify-between"><span>Convenience Fee</span><span>₹ 40</span></div>
                    <div class="flex justify-between text-emerald-600"><span>Discount</span><span>- ₹ 0</span></div>
                  </div>
                  <div class="mt-4 flex justify-between border-t border-slate-200 pt-3 text-xl font-bold text-blue-700"><span>Total Amount</span><span>₹ {{ totalAmount }}</span></div>
                </div>

                <div class="rounded-xl border border-blue-100 bg-blue-50 p-4">
                  <p class="font-semibold text-slate-900">Safe & Secure</p>
                  <p class="text-sm text-slate-600">Your payment is protected by 256-bit SSL encryption.</p>
                </div>
              </div>
            </div>

          <div class="mt-5 flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 pt-4">
            <p class="text-lg font-semibold text-slate-900">Total Amount to Pay <span class="ml-2 text-4xl text-blue-700">₹ {{ totalAmount }}</span></p>
            <button class="rounded-lg bg-blue-600 px-8 py-3 text-lg font-semibold text-white" (click)="payNow()">Pay Now ₹ {{ totalAmount }}</button>
          </div>
        </section>
      </div>
    </div>
  `
})
export class PaymentComponent implements OnInit {
  bus: Booking['bus'] | null = null;
  seats: string[] = [];
  passengers: Booking['passengers'] = [];

  constructor(
    readonly bookingService: BookingService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.bus = this.bookingService.selectedBus$.value;
    this.seats = this.bookingService.selectedSeats$.value;
    this.passengers = this.bookingService.passengerDetails$.value;

    if (!this.bus || this.seats.length === 0 || this.passengers.length === 0) {
      void this.router.navigate(['/user/buses']);
    }
  }

  get totalAmount(): number {
    if (!this.bus || this.seats.length === 0) {
      return 0;
    }

    return this.seats.length * this.bus.price + 40;
  }

  payNow(): void {
    const created = this.bookingService.createBooking();
    if (!created) {
      return;
    }

    void this.router.navigate(['/user/ticket']);
  }

  goBackToPassenger(): void {
    void this.router.navigate(['/user/passenger']);
  }

}
