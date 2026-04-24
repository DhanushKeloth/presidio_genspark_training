import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { BookingService } from '../../services/booking.service';

@Component({
  selector: 'app-ticket',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="min-h-screen bg-slate-100 px-4 py-6">
      <div class="mx-auto max-w-7xl" *ngIf="latestBooking as booking">
        <div class="grid gap-4 lg:grid-cols-[1fr_290px]">
          <section class="space-y-4">
            <div class="grid items-center gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm md:grid-cols-[90px_1fr_240px]">
              <div class="flex h-20 w-20 items-center justify-center rounded-full bg-emerald-100 text-5xl text-emerald-600">✓</div>
              <div>
                <h1 class="text-4xl font-bold text-slate-900">Your Booking is Confirmed!</h1>
                <p class="mt-1 text-sm text-slate-600">Thank you for traveling with us. We have sent the ticket to</p>
                <p class="text-sm font-semibold text-emerald-700">rahul.verma@example.com</p>
              </div>
              <div class="border-l border-slate-200 pl-4">
                <p class="text-sm text-slate-500">Booking ID</p>
                <p class="text-4xl font-bold text-emerald-700">{{ booking.id }}</p>
                <p class="mt-1 text-xs text-slate-500">Booked on {{ bookingDateTime }}</p>
              </div>
            </div>

            <div class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
              <div class="grid gap-4 md:grid-cols-[150px_1fr_180px] md:items-center">
                <div class="h-24 rounded-lg bg-slate-200"></div>
                <div>
                  <h2 class="text-4xl font-semibold text-slate-900">{{ booking.bus.name }}</h2>
                  <p class="text-sm text-slate-500">{{ booking.bus.type }}</p>
                  <p class="mt-2 text-xs text-slate-600">★ 4.5 &nbsp; 320 Reviews</p>
                </div>
                <div class="rounded-lg border border-blue-100 bg-blue-50 p-3 text-center">
                  <p class="text-xs font-semibold text-blue-700">PNR</p>
                  <p class="text-4xl font-bold text-blue-700">KPN987654</p>
                </div>
              </div>

              <div class="mt-4 rounded-xl border border-slate-200 p-4">
                <div class="grid items-center gap-3 text-center md:grid-cols-3">
                  <div class="text-left">
                    <p class="text-5xl font-bold text-slate-900">{{ booking.bus.departureTime }}</p>
                    <p class="text-sm text-slate-500">{{ booking.bus.date }}, Thu</p>
                    <p class="mt-1 text-2xl font-semibold text-slate-900">{{ booking.bus.from }}</p>
                    <p class="text-sm text-slate-500">Majestic</p>
                  </div>
                  <div>
                    <p class="text-sm text-slate-500">{{ booking.bus.duration }}</p>
                    <p class="text-sm text-slate-500">→</p>
                    <p class="text-sm text-slate-500">Non Stop</p>
                  </div>
                  <div class="text-right">
                    <p class="text-5xl font-bold text-slate-900">{{ booking.bus.arrivalTime }}</p>
                    <p class="text-sm text-slate-500">{{ booking.bus.date }}, Thu</p>
                    <p class="mt-1 text-2xl font-semibold text-slate-900">{{ booking.bus.to }}</p>
                    <p class="text-sm text-slate-500">CMBT</p>
                  </div>
                </div>
              </div>

              <div class="mt-4 grid gap-3 rounded-xl border border-slate-200 p-4 md:grid-cols-2">
                <div>
                  <p class="text-sm font-semibold text-slate-900">Selected Seats</p>
                  <div class="mt-2 flex flex-wrap gap-2">
                    <span *ngFor="let seat of booking.seats" class="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-1 text-sm font-semibold text-emerald-700">{{ seat }}</span>
                  </div>
                </div>
                <div>
                  <p class="text-sm font-semibold text-slate-900">Passengers ({{ booking.passengers.length }})</p>
                  <ol class="mt-2 space-y-1 text-sm text-slate-700">
                    <li *ngFor="let passenger of booking.passengers; index as i">{{ i + 1 }}. {{ passenger.name || 'Passenger' }} ({{ passenger.gender || '-' }}, {{ passenger.age || '-' }})</li>
                  </ol>
                </div>
              </div>

              <div class="mt-4 grid gap-3 rounded-xl border border-slate-200 p-4 text-sm md:grid-cols-4">
                <div>
                  <p class="font-semibold text-slate-900">Boarding Point</p>
                  <p class="mt-1 text-slate-600">Majestic, Bangalore</p>
                  <p class="text-slate-500">07:45 AM</p>
                </div>
                <div>
                  <p class="font-semibold text-slate-900">Dropping Point</p>
                  <p class="mt-1 text-slate-600">CMBT, Chennai</p>
                  <p class="text-slate-500">02:30 PM</p>
                </div>
                <div>
                  <p class="font-semibold text-slate-900">Bus Type</p>
                  <p class="mt-1 text-slate-600">{{ booking.bus.type }}</p>
                </div>
                <div>
                  <p class="font-semibold text-slate-900">Total Fare</p>
                  <p class="mt-1 text-4xl font-bold text-emerald-700">₹{{ booking.totalAmount }}</p>
                  <p class="text-slate-500">Paid Online</p>
                </div>
              </div>
            </div>
          </section>

          <aside class="space-y-4">
            <div class="grid grid-cols-3 gap-2 rounded-xl border border-slate-200 bg-white p-3 shadow-sm text-center text-xs font-medium text-slate-700">
              <button class="rounded-lg border border-slate-200 px-2 py-3">Download PDF</button>
              <button class="rounded-lg border border-slate-200 px-2 py-3">Print Ticket</button>
              <button class="rounded-lg border border-slate-200 px-2 py-3">Share</button>
            </div>

            <div class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
              <h3 class="text-lg font-semibold text-slate-900">Fare Summary</h3>
              <div class="mt-3 space-y-2 text-sm text-slate-700">
                <div class="flex justify-between"><span>Seat Fare (₹{{ booking.bus.price }} x {{ booking.seats.length }})</span><span>₹{{ booking.bus.price * booking.seats.length }}</span></div>
                <div class="flex justify-between"><span>Platform Fee</span><span>₹40</span></div>
              </div>
              <div class="mt-3 flex justify-between border-t border-slate-200 pt-3 text-xl font-bold text-slate-900"><span>Total Amount</span><span class="text-emerald-700">₹{{ booking.totalAmount }}</span></div>
              <div class="mt-3 rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-xs text-emerald-700">
                <p class="font-semibold">Payment Successful</p>
                <p>Paid Online via UPI</p>
                <p>{{ bookingDateTime }}</p>
              </div>
            </div>

            <div class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
              <h3 class="text-lg font-semibold text-slate-900">Need Help?</h3>
              <div class="mt-3 space-y-3 text-sm text-slate-700">
                <p><span class="font-medium">Call Us</span><br />080-1234-5678</p>
                <p><span class="font-medium">Email Us</span><br />support@busbooker.com</p>
                <p><span class="font-medium">Chat with Us</span><br />Available 24/7</p>
              </div>
            </div>
          </aside>
        </div>

        <section class="mt-4 rounded-xl border border-blue-100 bg-blue-50 p-4 text-sm text-slate-700">
          <p class="font-semibold text-blue-800">Important Information</p>
          <ul class="mt-2 list-disc pl-5">
            <li>Please reach the boarding point 15 minutes before departure.</li>
            <li>Carry a valid ID proof during travel.</li>
            <li>This is a computer generated ticket and does not require a signature.</li>
          </ul>
        </section>
      </div>
    </div>
  `
})
export class TicketComponent {
  constructor(
    private readonly bookingService: BookingService,
    private readonly router: Router
  ) {}

  get latestBooking() {
    return this.bookingService.bookings$.value[0] ?? null;
  }

  get bookingDateTime(): string {
    const booking = this.latestBooking;
    if (!booking) {
      return '';
    }

    return new Date(booking.bookingDate).toLocaleString();
  }

  goToBookings(): void {
    void this.router.navigate(['/user/bookings']);
  }
}
