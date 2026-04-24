import { Routes } from '@angular/router';
import { SearchComponent } from './pages/search/search.component';
import { BusListComponent } from './pages/bus-list/bus-list.component';
import { SeatSelectionComponent } from './pages/seat-selection/seat-selection.component';
import { PassengerFormComponent } from './pages/passenger-form/passenger-form.component';
import { PaymentComponent } from './pages/payment/payment.component';
import { TicketComponent } from './pages/ticket/ticket.component';
import { MyBookingsComponent } from './pages/my-bookings/my-bookings.component';
import { BookingDetailsComponent } from './pages/booking-details/booking-details.component';
import { ProfileComponent } from './pages/profile/profile.component';
import { AuthComponent } from './pages/auth/auth.component';

export const routes: Routes = [
  { path: '', redirectTo: 'user/search', pathMatch: 'full' },
  { path: 'auth', component: AuthComponent },
  {
    path: 'user',
    children: [
      { path: 'search', component: SearchComponent },
      { path: 'buses', component: BusListComponent },
      { path: 'seats', component: SeatSelectionComponent },
      { path: 'passenger', component: PassengerFormComponent },
      { path: 'payment', component: PaymentComponent },
      { path: 'ticket', component: TicketComponent },
      { path: 'bookings', component: MyBookingsComponent },
      { path: 'booking-details/:id', component: BookingDetailsComponent },
      { path: 'profile', component: ProfileComponent }
    ]
  },
  { path: '**', redirectTo: 'user/search' }
];
