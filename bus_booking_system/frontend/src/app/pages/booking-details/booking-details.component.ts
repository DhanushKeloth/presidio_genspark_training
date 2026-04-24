import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { BookingService } from '../../services/booking.service';
import { Booking } from '../../models/booking.models';

@Component({
  selector: 'app-booking-details',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="min-h-screen bg-slate-100 p-6">
      <div class="mx-auto max-w-4xl rounded-xl bg-white p-6 shadow-sm" *ngIf="booking; else notFound">
        <h1 class="text-2xl font-bold text-slate-900">Booking Details</h1>
        <p class="mt-2 text-slate-600">Booking ID: {{ booking.id }}</p>

        <div class="mt-6 grid gap-3 text-sm md:grid-cols-2">
          <div><span class="font-medium">Bus:</span> {{ booking.bus.name }}</div>
          <div><span class="font-medium">Trip:</span> {{ booking.bus.from }} → {{ booking.bus.to }}</div>
          <div><span class="font-medium">Seats:</span> {{ booking.seats.join(', ') }}</div>
          <div><span class="font-medium">Amount:</span> ₹{{ booking.totalAmount }}</div>
        </div>

        <div class="mt-6">
          <h2 class="font-semibold text-slate-900">Passengers</h2>
          <ul class="mt-2 space-y-1 text-sm text-slate-700">
            <li *ngFor="let passenger of booking.passengers">{{ passenger.name }} · {{ passenger.age }} · {{ passenger.gender }} · {{ passenger.seatNumber }}</li>
          </ul>
        </div>

        <div class="mt-6 flex gap-3">
          <button class="rounded-lg border border-red-200 px-4 py-2 text-red-700" (click)="cancelBooking()">Cancel Booking</button>
          <button class="rounded-lg border border-slate-300 px-4 py-2" (click)="downloadTicket()">Download Ticket</button>
        </div>
      </div>

      <ng-template #notFound>
        <div class="mx-auto max-w-xl rounded-xl bg-white p-6 text-center shadow-sm">
          <p>Booking not found.</p>
          <button class="mt-4 rounded-lg bg-blue-600 px-4 py-2 text-white" (click)="goToBookings()">Go to My Bookings</button>
        </div>
      </ng-template>
    </div>
  `
})
export class BookingDetailsComponent implements OnInit {
  booking: Booking | undefined;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly bookingService: BookingService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      return;
    }

    this.booking = this.bookingService.bookings$.value.find((booking) => booking.id === id);
  }

  cancelBooking(): void {
    if (!this.booking) {
      return;
    }

    this.booking.status = 'Cancelled';
    this.bookingService.bookings$.next([...this.bookingService.bookings$.value]);
  }

  downloadTicket(): void {
    window.alert('Ticket download is mocked in frontend-only mode.');
  }

  goToBookings(): void {
    void this.router.navigate(['/user/bookings']);
  }
}
