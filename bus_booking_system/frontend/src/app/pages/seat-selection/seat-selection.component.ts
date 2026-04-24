import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { BookingService } from '../../services/booking.service';
import { Bus } from '../../models/booking.models';

@Component({
  selector: 'app-seat-selection',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="min-h-screen bg-slate-100 px-4 py-5">
      <div class="mx-auto max-w-7xl">
        <div class="mb-4 flex items-center justify-between text-sm text-slate-700">
          <button class="flex items-center gap-2" (click)="goBackToBuses()">← Back to Results</button>
          <div class="flex items-center gap-5"><span>Help</span><span>My Bookings</span></div>
        </div>

        <div class="grid gap-4 lg:grid-cols-[260px_1fr_300px]" *ngIf="bus">
          <aside class="space-y-3 rounded-xl border border-slate-200 bg-white p-3">
            <div class="h-32 rounded-lg bg-slate-200"></div>
            <div>
              <div class="flex items-center gap-2">
                <h2 class="text-3xl font-semibold text-slate-900">{{ bus.name }}</h2>
                <span class="text-blue-600">●</span>
              </div>
              <p class="text-sm text-slate-500">{{ bus.type }}</p>
              <p class="mt-2 text-xs text-slate-600">★ 4.5 &nbsp; 320 Reviews</p>
              <p class="mt-1 text-xs text-slate-500">Wi-Fi &nbsp; Charging Point &nbsp; Blanket</p>
            </div>

            <div class="rounded-lg border border-slate-200 p-3">
              <div class="grid grid-cols-3 text-center">
                <div><p class="text-2xl font-semibold">{{ bus.departureTime }}</p><p class="text-xs text-slate-500">Majestic</p></div>
                <div><p class="text-xs text-slate-500">{{ bus.duration }}</p><p class="text-xs text-slate-500">Non Stop</p></div>
                <div><p class="text-2xl font-semibold">{{ bus.arrivalTime }}</p><p class="text-xs text-slate-500">CMBT</p></div>
              </div>
              <p class="mt-3 text-xs text-slate-500">{{ bus.date }}, Thursday</p>
            </div>

            <div class="rounded-lg border border-slate-200 p-3 text-sm">
              <p class="font-semibold text-slate-800">Boarding & Dropping Points</p>
              <p class="mt-2 text-slate-700">08:00 AM<br /><span class="text-xs text-slate-500">Majestic, Bangalore</span></p>
              <p class="mt-3 text-slate-700">02:30 PM<br /><span class="text-xs text-slate-500">CMBT, Chennai</span></p>
            </div>

            <button class="w-full rounded-lg border border-blue-200 py-2 text-sm font-medium text-blue-700">View Bus Photos</button>
          </aside>

          <section class="rounded-xl border border-slate-200 bg-white p-4">
            <div class="mb-4 flex items-center justify-center gap-5 text-sm">
              <div class="flex items-center gap-2"><span class="flex h-6 w-6 items-center justify-center rounded-full bg-blue-600 text-white">1</span><span class="font-semibold text-slate-900">Select Seats</span></div>
              <div class="h-px w-14 bg-slate-300"></div>
              <div class="flex items-center gap-2 text-slate-500"><span class="flex h-6 w-6 items-center justify-center rounded-full border border-slate-300">2</span><span>Passenger Details</span></div>
              <div class="h-px w-14 bg-slate-300"></div>
              <div class="flex items-center gap-2 text-slate-500"><span class="flex h-6 w-6 items-center justify-center rounded-full border border-slate-300">3</span><span>Payment</span></div>
            </div>

            <div class="mb-4 flex flex-wrap gap-5 rounded-lg border border-slate-200 px-4 py-3 text-sm">
              <span class="inline-flex items-center gap-2"><span class="h-3 w-3 rounded bg-emerald-300"></span>Available</span>
              <span class="inline-flex items-center gap-2"><span class="h-3 w-3 rounded bg-blue-500"></span>Selected</span>
              <span class="inline-flex items-center gap-2"><span class="h-3 w-3 rounded bg-rose-300"></span>Booked</span>
              <span class="inline-flex items-center gap-2"><span class="h-3 w-3 rounded bg-slate-300"></span>Blocked</span>
            </div>

            <div class="rounded-lg border border-slate-200 p-4">
              <div class="mb-3 text-center text-sm text-slate-500">Driver</div>
              <div class="grid grid-cols-5 gap-3">
                <ng-container *ngFor="let seat of seatMap; index as i">
                  <div *ngIf="i % 5 === 2" class="flex items-center justify-center text-sm text-slate-500">{{ rowNumber(i) }}</div>
                  <button
                    *ngIf="i % 5 !== 2"
                    class="h-11 rounded-lg border text-sm font-semibold"
                    [ngClass]="seatClass(seat)"
                    [disabled]="isBooked(seat) || isBlocked(seat)"
                    (click)="toggleSeat(seat)"
                  >
                    {{ seat }}
                  </button>
                </ng-container>
              </div>
            </div>

            <p class="mt-3 text-center text-xs text-slate-500">You can select maximum 4 seats</p>
          </section>

          <aside class="rounded-xl border border-slate-200 bg-white p-4">
            <div class="mb-4 flex items-center justify-between">
              <h2 class="text-lg font-semibold text-slate-900">Your Selection</h2>
              <button class="text-sm text-blue-700" (click)="clearSelection()">Clear All</button>
            </div>

            <p class="text-sm font-medium text-slate-800">Selected Seats ({{ selectedSeats.length }})</p>
            <div class="mt-2 flex flex-wrap gap-2">
              <span *ngFor="let seat of selectedSeats" class="rounded-md border border-blue-200 bg-blue-50 px-3 py-1 text-sm font-medium text-blue-700">{{ seat }}</span>
              <span *ngIf="selectedSeats.length === 0" class="text-sm text-slate-500">No seats selected</span>
            </div>

            <div class="mt-5 border-t border-slate-200 pt-4 text-sm">
              <p class="font-semibold text-slate-900">Trip Summary</p>
              <p class="mt-2 text-slate-700">{{ bus.name }}<br /><span class="text-xs text-slate-500">{{ bus.type }}</span></p>
              <p class="mt-3 text-slate-700">{{ bus.from }}<br /><span class="text-xs text-slate-500">Majestic</span></p>
              <p class="mt-3 text-slate-700">{{ bus.to }}<br /><span class="text-xs text-slate-500">CMBT</span></p>
              <p class="mt-3 text-slate-700">{{ bus.date }}, Thursday</p>
              <p class="mt-1 text-slate-700">{{ bus.departureTime }} – {{ bus.arrivalTime }}<br /><span class="text-xs text-slate-500">{{ bus.duration }} (Non Stop)</span></p>
            </div>

            <div class="mt-5 border-t border-slate-200 pt-4 text-sm">
              <p class="font-semibold text-slate-900">Price Details</p>
              <div class="mt-2 flex justify-between"><span>Seat Fare (₹{{ bus.price }} x {{ selectedSeats.length }})</span><span>₹{{ bus.price * selectedSeats.length }}</span></div>
              <div class="mt-1 flex justify-between"><span>Platform Fee</span><span>₹40</span></div>
              <div class="mt-3 flex justify-between text-xl font-bold text-slate-900"><span>Total Amount</span><span class="text-emerald-600">₹{{ totalAmount }}</span></div>
            </div>

            <div class="mt-4 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-700">Seats are temporarily locked for 09:47</div>

            <button
              class="mt-4 w-full rounded-lg bg-blue-600 px-4 py-3 font-semibold text-white disabled:opacity-50"
              [disabled]="selectedSeats.length === 0"
              (click)="continueToPassenger()"
            >
              Continue to Passenger Details
            </button>
          </aside>
        </div>
      </div>
    </div>
  `
})
export class SeatSelectionComponent implements OnInit {
  bus: Bus | null = null;
  selectedSeats: string[] = [];
  readonly blockedSeats: string[] = ['A16'];
  readonly seatMap: string[] = [
    'A1', 'A2', '', 'B1', 'B2',
    'A3', 'A4', '', 'B3', 'B4',
    'A5', 'A6', '', 'B5', 'B6',
    'A7', 'A8', '', 'B7', 'B8',
    'A9', 'A10', '', 'B9', 'B10',
    'A11', 'A12', '', 'B11', 'B12',
    'A13', 'A14', '', 'B13', 'B14',
    'A15', 'A16', '', 'B15', 'B16',
    'A17', 'A18', '', 'B17', 'B18'
  ];

  constructor(
    private readonly router: Router,
    private readonly bookingService: BookingService
  ) {}

  ngOnInit(): void {
    this.bus = this.bookingService.selectedBus$.value;
    if (!this.bus) {
      void this.router.navigate(['/user/buses']);
      return;
    }

    this.selectedSeats = [...this.bookingService.selectedSeats$.value];
  }

  rowNumber(index: number): number {
    return Math.floor(index / 5) + 1;
  }

  isBooked(seat: string): boolean {
    return !!seat && !!this.bus?.bookedSeats.includes(seat);
  }

  isBlocked(seat: string): boolean {
    return !!seat && this.blockedSeats.includes(seat);
  }

  isSelected(seat: string): boolean {
    return this.selectedSeats.includes(seat);
  }

  seatClass(seat: string): string {
    if (this.isSelected(seat)) {
      return 'border-blue-500 bg-blue-500 text-white';
    }

    if (this.isBlocked(seat)) {
      return 'border-slate-300 bg-slate-200 text-slate-500';
    }

    if (this.isBooked(seat)) {
      return 'border-rose-200 bg-rose-100 text-rose-500';
    }

    return 'border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100';
  }

  toggleSeat(seat: string): void {
    if (!seat || this.isBooked(seat) || this.isBlocked(seat)) {
      return;
    }

    if (this.isSelected(seat)) {
      this.selectedSeats = this.selectedSeats.filter((value) => value !== seat);
    } else {
      if (this.selectedSeats.length >= 4) {
        return;
      }
      this.selectedSeats = [...this.selectedSeats, seat];
    }

    this.bookingService.setSelectedSeats(this.selectedSeats);
  }

  clearSelection(): void {
    this.selectedSeats = [];
    this.bookingService.setSelectedSeats([]);
  }

  get totalAmount(): number {
    if (!this.bus) {
      return 0;
    }

    return this.selectedSeats.length * this.bus.price + 40;
  }

  continueToPassenger(): void {
    if (this.selectedSeats.length === 0) {
      return;
    }

    this.bookingService.setSelectedSeats(this.selectedSeats);
    void this.router.navigate(['/user/passenger']);
  }

  goBackToBuses(): void {
    void this.router.navigate(['/user/buses']);
  }
}
