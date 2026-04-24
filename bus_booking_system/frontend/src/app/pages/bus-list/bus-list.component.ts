import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { BookingService } from '../../services/booking.service';
import { Bus } from '../../models/booking.models';

@Component({
  selector: 'app-bus-list',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="min-h-screen bg-slate-100 px-4 py-5">
      <div class="mx-auto max-w-7xl">
        <section class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
          <div class="grid gap-3 md:grid-cols-4">
            <div class="rounded-lg border border-slate-200 p-3">
              <p class="text-sm font-semibold text-slate-900">{{ source }}</p>
              <p class="text-xs text-slate-500">Majestic</p>
            </div>
            <div class="rounded-lg border border-slate-200 p-3">
              <p class="text-sm font-semibold text-slate-900">{{ destination }}</p>
              <p class="text-xs text-slate-500">CMBT</p>
            </div>
            <div class="rounded-lg border border-slate-200 p-3">
              <p class="text-sm font-semibold text-slate-900">15 May 2025</p>
              <p class="text-xs text-slate-500">Thursday</p>
            </div>
            <div class="flex items-center justify-end">
              <button class="rounded-lg border border-blue-200 px-4 py-2 text-sm font-medium text-blue-700">Modify Search</button>
            </div>
          </div>
        </section>

        <section class="mt-4 grid gap-4 lg:grid-cols-[250px_1fr]">
          <aside class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <div class="mb-4 flex items-center justify-between">
              <p class="text-lg font-semibold text-slate-900">Filters</p>
              <button class="text-sm text-blue-700">Clear All</button>
            </div>

            <div class="space-y-5 text-sm">
              <div>
                <p class="mb-2 font-semibold text-slate-800">Price</p>
                <div class="h-1 rounded bg-slate-200"></div>
                <div class="mt-2 flex justify-between text-xs text-slate-500"><span>₹300</span><span>₹1500+</span></div>
              </div>
              <div>
                <p class="mb-2 font-semibold text-slate-800">Departure Time</p>
                <div class="space-y-1 text-slate-600">
                  <p><input type="checkbox" /> Before 6 AM</p>
                  <p><input type="checkbox" /> 6 AM - 12 PM</p>
                  <p><input type="checkbox" /> 12 PM - 6 PM</p>
                  <p><input type="checkbox" /> After 6 PM</p>
                </div>
              </div>
              <div>
                <p class="mb-2 font-semibold text-slate-800">Bus Type</p>
                <div class="space-y-1 text-slate-600">
                  <p><input type="checkbox" /> AC Seater</p>
                  <p><input type="checkbox" /> Non-AC Seater</p>
                  <p><input type="checkbox" /> AC Sleeper</p>
                  <p><input type="checkbox" /> Non-AC Sleeper</p>
                </div>
              </div>
              <button class="w-full rounded-lg border border-blue-200 px-3 py-2 font-medium text-blue-700">Apply Filters</button>
            </div>
          </aside>

          <div>
            <div class="mb-3 flex items-center justify-between">
              <p class="text-lg font-semibold text-slate-900">{{ buses.length }} Buses Found</p>
              <div class="flex items-center gap-2 text-sm">
                <span class="text-slate-500">Sort by:</span>
                <select class="rounded-md border border-slate-300 px-3 py-1.5">
                  <option>Departure Time</option>
                </select>
              </div>
            </div>

            <div class="space-y-3">
              <article *ngFor="let bus of buses" class="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
                <div class="grid gap-4 md:grid-cols-[120px_1.6fr_1fr_140px_150px] md:items-center">
                  <div class="h-20 rounded-lg bg-slate-200"></div>
                  <div>
                    <div class="flex items-center gap-1">
                      <h2 class="text-2xl font-semibold text-slate-900">{{ bus.name }}</h2>
                      <span class="text-blue-600">●</span>
                    </div>
                    <p class="text-sm text-slate-500">{{ bus.type }}</p>
                    <p class="mt-1 text-xs text-slate-600">★ 4.5 &nbsp; 320 Reviews</p>
                    <p class="mt-1 text-xs text-slate-500">Wi-Fi &nbsp; Charging Point &nbsp; Blanket</p>
                  </div>
                  <div class="grid grid-cols-3 items-center text-center text-sm">
                    <div>
                      <p class="text-3xl font-semibold text-slate-900">{{ bus.departureTime }}</p>
                      <p class="text-xs text-slate-500">Majestic</p>
                    </div>
                    <div>
                      <p class="text-xs text-slate-500">{{ bus.duration }}</p>
                      <p class="text-xs text-slate-500">Non Stop</p>
                    </div>
                    <div>
                      <p class="text-3xl font-semibold text-slate-900">{{ bus.arrivalTime }}</p>
                      <p class="text-xs text-slate-500">CMBT</p>
                    </div>
                  </div>
                  <div class="text-center">
                    <p class="text-4xl font-bold text-slate-900">₹{{ bus.price }}</p>
                    <p class="text-xs text-slate-500">per seat</p>
                    <p class="mt-2 text-sm font-medium text-emerald-600">{{ bus.availableSeats }} seats available</p>
                  </div>
                  <div class="text-right">
                    <button class="w-full rounded-lg bg-blue-600 px-4 py-2.5 font-semibold text-white" (click)="viewSeats(bus)">View Seats</button>
                  </div>
                </div>
              </article>
            </div>

            <div class="mt-4 flex items-center justify-center gap-2 text-sm">
              <button class="rounded border border-slate-300 px-3 py-1.5 text-slate-500">Previous</button>
              <button class="rounded bg-blue-600 px-3 py-1.5 text-white">1</button>
              <button class="rounded border border-slate-300 px-3 py-1.5">2</button>
              <button class="rounded border border-slate-300 px-3 py-1.5">3</button>
              <button class="rounded border border-slate-300 px-3 py-1.5">4</button>
              <button class="rounded border border-slate-300 px-3 py-1.5">5</button>
              <button class="rounded border border-slate-300 px-3 py-1.5">Next</button>
            </div>
          </div>
        </section>

        <section class="mt-4 rounded-xl border border-slate-200 bg-white p-4">
          <div class="grid grid-cols-2 gap-3 text-center text-sm font-medium text-slate-700 md:grid-cols-4">
            <p>Secure Payments</p>
            <p>24/7 Customer Support</p>
            <p>Easy Cancellation</p>
            <p>Best Price Guarantee</p>
          </div>
        </section>
      </div>
    </div>
  `
})
export class BusListComponent {
  readonly buses: Bus[];
  readonly source: string;
  readonly destination: string;

  constructor(
    private readonly bookingService: BookingService,
    private readonly router: Router
  ) {
    this.buses = this.bookingService.getBuses();
    this.source = this.bookingService.searchPayload$.value?.source || 'Bangalore (KA)';
    this.destination = this.bookingService.searchPayload$.value?.destination || 'Chennai (TN)';
  }

  viewSeats(bus: Bus): void {
    this.bookingService.setSelectedBus(bus);
    this.bookingService.setSelectedSeats([]);
    void this.router.navigate(['/user/seats']);
  }
}
