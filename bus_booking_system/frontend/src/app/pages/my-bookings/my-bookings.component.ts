import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { BookingService } from '../../services/booking.service';

@Component({
  selector: 'app-my-bookings',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="min-h-screen bg-slate-100 p-6">
      <div class="mx-auto max-w-5xl rounded-xl bg-white p-6 shadow-sm">
        <h1 class="text-2xl font-bold text-slate-900">My Bookings</h1>

        <div class="mt-6 space-y-4" *ngIf="bookingService.bookings$.value.length; else emptyState">
          <article *ngFor="let booking of bookingService.bookings$.value" class="rounded-lg border border-slate-200 p-4">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <h2 class="text-lg font-semibold">{{ booking.bus.name }}</h2>
              <span class="rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-700">{{ booking.status }}</span>
            </div>
            <p class="mt-1 text-sm text-slate-600">{{ booking.bus.from }} → {{ booking.bus.to }} · Seats: {{ booking.seats.join(', ') }}</p>
            <button class="mt-3 rounded-lg border border-blue-200 px-4 py-2 text-blue-700" (click)="viewBooking(booking.id)">View Booking</button>
          </article>
        </div>

        <ng-template #emptyState>
          <p class="mt-6 text-slate-600">No bookings found. Start from search and complete a booking.</p>
        </ng-template>
      </div>
    </div>
  `
})
export class MyBookingsComponent {
  constructor(
    readonly bookingService: BookingService,
    private readonly router: Router
  ) {}

  viewBooking(id: string): void {
    void this.router.navigate(['/user/booking-details', id]);
  }
}
