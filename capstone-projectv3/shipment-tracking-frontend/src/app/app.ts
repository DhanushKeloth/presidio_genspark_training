import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Login } from "./auth/pages/login/login";
import { RegisterCustomer } from "./auth/pages/register-customer/register-customer";
import { RegisterDriver } from "./auth/pages/register-driver/register-driver";
import { CustomerSidebar } from "./shared/components/customer-sidebar/customer-sidebar";
import { Navbar } from "./shared/components/navbar/navbar";
import { CustomerLayout } from './core/layouts/customer-layout/customer-layout';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Login, RegisterCustomer, RegisterDriver, CustomerSidebar, Navbar,],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly title = signal('shipment-tracking-frontend');
}
