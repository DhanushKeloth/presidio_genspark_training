import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { Booking, Bus, Passenger, SearchPayload } from '../models/booking.models';

@Injectable({ providedIn: 'root' })
export class BookingService {
  readonly searchPayload$ = new BehaviorSubject<SearchPayload | null>(null);
  readonly selectedBus$ = new BehaviorSubject<Bus | null>(null);
  readonly selectedSeats$ = new BehaviorSubject<string[]>([]);
  readonly passengerDetails$ = new BehaviorSubject<Passenger[]>([]);
  readonly bookings$ = new BehaviorSubject<Booking[]>([]);

  private readonly mockBuses: Bus[] = [
    {
      id: 'KPN123',
      name: 'KPN Travels',
      type: 'AC Sleeper (2+1)',
      operator: 'KPN',
      from: 'Bangalore (KA)',
      to: 'Chennai (TN)',
      departureTime: '08:00 AM',
      arrivalTime: '02:30 PM',
      date: '15 May 2025',
      duration: '6h 30m',
      price: 750,
      availableSeats: 18,
      bookedSeats: ['B3', 'A7', 'A11', 'A16', 'B7']
    },
    {
      id: 'SRS456',
      name: 'SRS Travels',
      type: 'AC Seater (2+2)',
      operator: 'SRS',
      from: 'Bangalore (KA)',
      to: 'Chennai (TN)',
      departureTime: '09:30 AM',
      arrivalTime: '03:45 PM',
      date: '15 May 2025',
      duration: '6h 15m',
      price: 600,
      availableSeats: 22,
      bookedSeats: ['A5', 'A8', 'B1', 'B8']
    },
    {
      id: 'ORG789',
      name: 'Orange Tours',
      type: 'Non-AC Seater (2+2)',
      operator: 'Orange',
      from: 'Bangalore (KA)',
      to: 'Chennai (TN)',
      departureTime: '11:00 AM',
      arrivalTime: '05:45 PM',
      date: '15 May 2025',
      duration: '6h 45m',
      price: 450,
      availableSeats: 31,
      bookedSeats: ['A2', 'A9', 'B4']
    },
    {
      id: 'VRL321',
      name: 'VRL Travels',
      type: 'AC Sleeper (2+1)',
      operator: 'VRL',
      from: 'Bangalore (KA)',
      to: 'Chennai (TN)',
      departureTime: '10:30 PM',
      arrivalTime: '04:50 AM',
      date: '15 May 2025',
      duration: '6h 20m',
      price: 800,
      availableSeats: 12,
      bookedSeats: ['A1', 'A4', 'B3', 'B6', 'B9']
    }
  ];

  getBuses(): Bus[] {
    return this.mockBuses;
  }

  getBusById(busId: string): Bus | undefined {
    return this.mockBuses.find((bus) => bus.id === busId);
  }

  setSearchPayload(payload: SearchPayload): void {
    this.searchPayload$.next(payload);
  }

  setSelectedBus(bus: Bus): void {
    this.selectedBus$.next(bus);
  }

  setSelectedSeats(seats: string[]): void {
    this.selectedSeats$.next(seats);
  }

  setPassengerDetails(passengers: Passenger[]): void {
    this.passengerDetails$.next(passengers);
  }

  createBooking(): Booking | null {
    const bus = this.selectedBus$.value;
    const seats = this.selectedSeats$.value;
    const passengers = this.passengerDetails$.value;

    if (!bus || seats.length === 0 || passengers.length === 0) {
      return null;
    }

    const booking: Booking = {
      id: `BB${Date.now().toString().slice(-8)}`,
      bus,
      seats,
      passengers,
      totalAmount: seats.length * bus.price + 40,
      paymentStatus: 'Paid',
      bookingDate: new Date().toISOString(),
      status: 'Upcoming'
    };

    this.bookings$.next([booking, ...this.bookings$.value]);
    return booking;
  }

  clearInProgressBooking(): void {
    this.selectedBus$.next(null);
    this.selectedSeats$.next([]);
    this.passengerDetails$.next([]);
  }
}
