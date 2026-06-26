import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import {MatIconModule} from '@angular/material/icon'

@Component({
  selector: 'app-customer-sidebar',
  imports: [RouterModule,MatIconModule],
  templateUrl: './customer-sidebar.html',
  styleUrl: './customer-sidebar.css',
})
export class CustomerSidebar {
   menuItems = [
    {
      label: 'Dashboard',
      icon: 'dashboard',
      route: '/customer/dashboard'
    },
    {
      label: 'Book Shipment',
      icon: 'add_box',
      route: '/customer/book-shipment'
    },
    {
      label: 'Shipments',
      icon: 'inventory_2',
      route: '/customer/shipments'
    },
    {
      label: 'Track Package',
      icon: 'local_shipping',
      route: '/customer/track'
    },
    {
      label: 'Profile',
      icon: 'person',
      route: '/customer/profile'
    },
    {
      label: 'Help & Support',
      icon: 'support_agent',
      route: '/customer/help'
    }
  ];
}
