import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { BookingService } from '../../services/booking.service';

@Component({
  selector: 'app-search',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="min-h-screen bg-slate-100 pb-10">
      <section class="mx-auto max-w-7xl px-4 pt-6">
        <div class="overflow-hidden rounded-2xl bg-gradient-to-r from-amber-100 via-orange-50 to-slate-100 shadow-sm">
          <div class="grid items-center gap-8 p-8 lg:grid-cols-2 lg:p-10">
            <div>
              <h1 class="text-4xl font-bold leading-tight text-slate-900 md:text-5xl">
                Book Bus Tickets
                <br />
                Anytime, Anywhere
              </h1>
              <p class="mt-4 max-w-xl text-lg text-slate-600">
                Find the best buses, compare prices and book your tickets with ease.
              </p>
            </div>

            <div class="rounded-2xl border-2 border-dashed border-slate-300 bg-white/70 p-6 text-center">
              <p class="text-sm font-medium text-slate-600">Hero Image Placeholder</p>
              <p class="mt-1 text-xs text-slate-500">Add bus/banner image here</p>
              <div class="mt-4 h-44 rounded-xl bg-slate-200"></div>
            </div>
          </div>
        </div>

        <form
          class="-mt-8 grid gap-3 rounded-2xl bg-white p-4 shadow-lg md:grid-cols-5 md:gap-4"
          [formGroup]="searchForm"
          (ngSubmit)="onSearch()"
        >
          <input class="rounded-lg border border-slate-300 px-4 py-3" placeholder="Select Source" formControlName="source" />
          <input class="rounded-lg border border-slate-300 px-4 py-3" placeholder="Select Destination" formControlName="destination" />
          <input class="rounded-lg border border-slate-300 px-4 py-3" type="date" formControlName="date" />
          <select class="rounded-lg border border-slate-300 px-4 py-3">
            <option>1 Passenger</option>
            <option>2 Passengers</option>
            <option>3 Passengers</option>
            <option>4 Passengers</option>
          </select>
          <button
            class="rounded-lg bg-blue-600 px-4 py-3 font-semibold text-white disabled:opacity-50"
            [disabled]="searchForm.invalid"
            type="submit"
          >
            Search Buses
          </button>
        </form>

        <div class="mt-6 grid gap-3 rounded-2xl bg-white p-4 shadow-sm md:grid-cols-4">
          <div class="rounded-xl border border-slate-200 p-4 text-center">
            <p class="font-semibold text-slate-900">Safe & Secure</p>
            <p class="mt-1 text-sm text-slate-600">Your safety is our top priority</p>
          </div>
          <div class="rounded-xl border border-slate-200 p-4 text-center">
            <p class="font-semibold text-slate-900">Best Prices</p>
            <p class="mt-1 text-sm text-slate-600">Compare and get the best deals</p>
          </div>
          <div class="rounded-xl border border-slate-200 p-4 text-center">
            <p class="font-semibold text-slate-900">Wide Choices</p>
            <p class="mt-1 text-sm text-slate-600">Choose from a wide range of buses</p>
          </div>
          <div class="rounded-xl border border-slate-200 p-4 text-center">
            <p class="font-semibold text-slate-900">24/7 Support</p>
            <p class="mt-1 text-sm text-slate-600">We are here to help anytime</p>
          </div>
        </div>

        <section class="mt-8">
          <div class="mb-4 flex items-center justify-between">
            <h2 class="text-2xl font-bold text-slate-900">Popular Routes</h2>
            <button class="text-sm font-semibold text-blue-700">View all</button>
          </div>

          <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <article class="rounded-xl bg-white p-4 shadow-sm">
              <h3 class="text-xl font-semibold text-slate-900">Bangalore</h3>
              <p class="text-slate-700">to Chennai</p>
              <p class="mt-2 text-sm text-slate-500">6h 15m</p>
              <p class="mt-2 font-semibold text-blue-700">From ₹600</p>
              <div class="mt-3 rounded-lg border-2 border-dashed border-slate-300 p-3 text-center text-xs text-slate-500">
                Route Image Placeholder
              </div>
            </article>

            <article class="rounded-xl bg-white p-4 shadow-sm">
              <h3 class="text-xl font-semibold text-slate-900">Hyderabad</h3>
              <p class="text-slate-700">to Vizag</p>
              <p class="mt-2 text-sm text-slate-500">6h 45m</p>
              <p class="mt-2 font-semibold text-blue-700">From ₹700</p>
              <div class="mt-3 rounded-lg border-2 border-dashed border-slate-300 p-3 text-center text-xs text-slate-500">
                Route Image Placeholder
              </div>
            </article>

            <article class="rounded-xl bg-white p-4 shadow-sm">
              <h3 class="text-xl font-semibold text-slate-900">Mumbai</h3>
              <p class="text-slate-700">to Pune</p>
              <p class="mt-2 text-sm text-slate-500">3h 30m</p>
              <p class="mt-2 font-semibold text-blue-700">From ₹500</p>
              <div class="mt-3 rounded-lg border-2 border-dashed border-slate-300 p-3 text-center text-xs text-slate-500">
                Route Image Placeholder
              </div>
            </article>

            <article class="rounded-xl bg-white p-4 shadow-sm">
              <h3 class="text-xl font-semibold text-slate-900">Delhi</h3>
              <p class="text-slate-700">to Jaipur</p>
              <p class="mt-2 text-sm text-slate-500">5h 30m</p>
              <p class="mt-2 font-semibold text-blue-700">From ₹650</p>
              <div class="mt-3 rounded-lg border-2 border-dashed border-slate-300 p-3 text-center text-xs text-slate-500">
                Route Image Placeholder
              </div>
            </article>
          </div>
        </section>
      </section>
    </div>
  `
})
export class SearchComponent {
  readonly searchForm: FormGroup;

  constructor(
    private readonly fb: FormBuilder,
    private readonly router: Router,
    private readonly bookingService: BookingService
  ) {
    this.searchForm = this.fb.group({
      source: ['Bangalore (KA)', Validators.required],
      destination: ['Chennai (TN)', Validators.required],
      date: ['', Validators.required]
    });
  }

  onSearch(): void {
    if (this.searchForm.invalid) {
      return;
    }

    this.bookingService.setSearchPayload(this.searchForm.getRawValue() as { source: string; destination: string; date: string });
    void this.router.navigate(['/user/buses']);
  }
}
