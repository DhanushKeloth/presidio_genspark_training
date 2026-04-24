export interface Bus {
  id: string;
  name: string;
  type: string;
  operator: string;
  from: string;
  to: string;
  departureTime: string;
  arrivalTime: string;
  date: string;
  duration: string;
  price: number;
  availableSeats: number;
  bookedSeats: string[];
}

export interface SearchPayload {
  source: string;
  destination: string;
  date: string;
}

export interface Passenger {
  seatNumber: string;
  name: string;
  age: number | null;
  gender: string;
}

export interface Booking {
  id: string;
  bus: Bus;
  seats: string[];
  passengers: Passenger[];
  totalAmount: number;
  paymentStatus: 'Paid' | 'Pending';
  bookingDate: string;
  status: 'Upcoming' | 'Completed' | 'Cancelled';
}
