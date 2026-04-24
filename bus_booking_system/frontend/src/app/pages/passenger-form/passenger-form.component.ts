import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Bus, Passenger } from '../../models/booking.models';
import { BookingService } from '../../services/booking.service';

@Component({
  selector: 'app-passenger-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="min-h-screen bg-slate-100 px-4 py-5" *ngIf="bus">
      <div class="mx-auto max-w-7xl">
        <div class="mb-4 flex items-center justify-center gap-5 text-sm">
          <div class="flex items-center gap-2 text-emerald-700"><span class="flex h-6 w-6 items-center justify-center rounded-full bg-emerald-600 text-white">✓</span><span class="font-semibold">Select Seats</span></div>
          <div class="h-px w-14 bg-slate-300"></div>
          <div class="flex items-center gap-2"><span class="flex h-6 w-6 items-center justify-center rounded-full bg-blue-600 text-white">2</span><span class="font-semibold text-slate-900">Passenger Details</span></div>
          <div class="h-px w-14 bg-slate-300"></div>
          <div class="flex items-center gap-2 text-slate-500"><span class="flex h-6 w-6 items-center justify-center rounded-full border border-slate-300">3</span><span>Payment</span></div>
        </div>

        <div class="grid gap-4 lg:grid-cols-[220px_1fr_250px]">
          <aside class="rounded-xl border border-slate-200 bg-white p-4">
            <button class="mb-3 text-sm text-blue-700" (click)="goBack()">← Back to Seat Selection</button>
            <p class="mb-3 font-semibold text-slate-900">Trip Summary</p>
            <div class="mb-3 h-24 rounded-lg bg-slate-200"></div>

            <h3 class="text-xl font-semibold text-slate-900">{{ bus.name }}</h3>
            <p class="text-sm text-slate-500">{{ bus.type }}</p>
            <p class="mt-2 text-xs text-slate-600">★ 4.5 &nbsp; 320 Reviews</p>

            <div class="mt-4 text-sm text-slate-700">
              <p class="font-medium">{{ bus.from }}</p>
              <p class="text-xs text-slate-500">Majestic</p>
              <p class="mt-3 font-medium">{{ bus.to }}</p>
              <p class="text-xs text-slate-500">CMBT</p>
            </div>

            <div class="mt-4 border-t border-slate-200 pt-3 text-sm text-slate-700">
              <p>{{ bus.date }}, Thursday</p>
              <p class="mt-2">{{ bus.departureTime }} - {{ bus.arrivalTime }}</p>
              <p class="text-xs text-slate-500">{{ bus.duration }} (Non Stop)</p>
            </div>

            <div class="mt-4 border-t border-slate-200 pt-3">
              <p class="text-sm font-medium text-slate-800">Selected Seats ({{ selectedSeats.length }})</p>
              <div class="mt-2 flex flex-wrap gap-2">
                <span *ngFor="let seat of selectedSeats" class="rounded-md border border-blue-200 bg-blue-50 px-3 py-1 text-sm font-medium text-blue-700">{{ seat }}</span>
              </div>
            </div>
          </aside>

          <section>
            <h1 class="text-4xl font-bold text-slate-900">Enter Passenger Details</h1>
            <p class="mt-1 text-sm text-slate-600">Please provide details for each passenger</p>

            <form class="mt-4 space-y-4" [formGroup]="form" (ngSubmit)="proceedToPayment()">
              <div formArrayName="passengers" *ngFor="let group of passengerArray.controls; let i = index" class="rounded-xl border border-slate-200 bg-white p-4">
                <p class="mb-3 text-sm font-semibold text-blue-700">Passenger {{ i + 1 }} &nbsp; • &nbsp; Seat {{ selectedSeats[i] }}</p>
                <div class="grid gap-3 md:grid-cols-3" [formGroupName]="i">
                  <div>
                    <label class="mb-1 block text-sm font-medium text-slate-700">Full Name*</label>
                    <input class="w-full rounded-lg border border-slate-300 px-3 py-2.5" placeholder="Enter full name" formControlName="name" />
                  </div>
                  <div>
                    <label class="mb-1 block text-sm font-medium text-slate-700">Age*</label>
                    <input class="w-full rounded-lg border border-slate-300 px-3 py-2.5" type="number" placeholder="Enter age" formControlName="age" />
                  </div>
                  <div>
                    <label class="mb-1 block text-sm font-medium text-slate-700">Gender*</label>
                    <select class="w-full rounded-lg border border-slate-300 px-3 py-2.5" formControlName="gender">
                      <option value="">Select gender</option>
                      <option value="Male">Male</option>
                      <option value="Female">Female</option>
                      <option value="Other">Other</option>
                    </select>
                  </div>
                  <div>
                    <label class="mb-1 block text-sm font-medium text-slate-700">Phone Number (Optional)</label>
                    <input class="w-full rounded-lg border border-slate-300 px-3 py-2.5" placeholder="Enter 10-digit mobile number" formControlName="phone" />
                  </div>
                  <div class="md:col-span-2">
                    <label class="mb-1 block text-sm font-medium text-slate-700">Email (Optional)</label>
                    <input class="w-full rounded-lg border border-slate-300 px-3 py-2.5" placeholder="Enter email address" formControlName="email" />
                  </div>
                </div>
              </div>

              <div class="rounded-lg border border-blue-100 bg-blue-50 px-3 py-2 text-sm text-blue-700">
                Ensure all passenger details match valid ID proof for a hassle-free journey.
              </div>

              <div class="mt-2 flex items-center justify-between gap-3">
                <button class="rounded-lg border border-slate-300 px-6 py-2.5 font-medium text-slate-700" type="button" (click)="goBack()">← Back to Seat Selection</button>
                <button class="rounded-lg bg-blue-600 px-8 py-2.5 font-semibold text-white disabled:opacity-50" [disabled]="form.invalid" type="submit">Proceed to Payment →</button>
              </div>
            </form>
          </section>

          <aside class="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
            <h2 class="text-lg font-semibold text-slate-900">Fare Summary</h2>
            <div class="text-sm text-slate-700">
              <div class="flex justify-between"><span>Seat Fare (₹{{ bus.price }} x {{ selectedSeats.length }})</span><span>₹{{ bus.price * selectedSeats.length }}</span></div>
              <div class="mt-2 flex justify-between"><span>Platform Fee</span><span>₹40</span></div>
              <div class="mt-4 flex justify-between border-t border-slate-200 pt-3 text-2xl font-bold text-slate-900"><span>Total Amount</span><span class="text-emerald-600">₹{{ totalAmount }}</span></div>
            </div>

            <div class="rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-700">Seats are temporarily locked for 08:45</div>

            <div class="space-y-3 border-t border-slate-200 pt-3 text-sm text-slate-700">
              <p><span class="font-medium">Secure Payments</span><br /><span class="text-xs text-slate-500">100% secure and encrypted</span></p>
              <p><span class="font-medium">Instant Confirmation</span><br /><span class="text-xs text-slate-500">Tickets sent to your email</span></p>
              <p><span class="font-medium">24/7 Customer Support</span><br /><span class="text-xs text-slate-500">We're here to help you anytime</span></p>
            </div>
          </aside>
        </div>
      </div>
    </div>
  `
})
export class PassengerFormComponent implements OnInit {
  readonly form: FormGroup;
  selectedSeats: string[] = [];
  bus: Bus | null = null;

  constructor(
    private readonly fb: FormBuilder,
    private readonly router: Router,
    private readonly bookingService: BookingService
  ) {
    this.form = this.fb.group({
      passengers: this.fb.array([])
    });
  }

  get passengerArray(): FormArray {
    return this.form.get('passengers') as FormArray;
  }

  get totalAmount(): number {
    if (!this.bus) {
      return 0;
    }

    return this.bus.price * this.selectedSeats.length + 40;
  }

  ngOnInit(): void {
    this.selectedSeats = this.bookingService.selectedSeats$.value;
    this.bus = this.bookingService.selectedBus$.value;

    if (this.selectedSeats.length === 0 || !this.bus) {
      void this.router.navigate(['/user/buses']);
      return;
    }

    this.selectedSeats.forEach((seat) => {
      this.passengerArray.push(this.fb.group({
        seatNumber: [seat],
        name: ['', Validators.required],
        age: [null, Validators.required],
        gender: ['', Validators.required],
        phone: [''],
        email: ['']
      }));
    });
  }

  proceedToPayment(): void {
    if (this.form.invalid) {
      return;
    }

    const passengers = this.form.getRawValue().passengers as Passenger[];
    this.bookingService.setPassengerDetails(passengers);
    void this.router.navigate(['/user/payment']);
  }

  goBack(): void {
    void this.router.navigate(['/user/seats']);
  }
}
