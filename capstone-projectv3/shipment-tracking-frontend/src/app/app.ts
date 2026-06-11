import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Login } from "./auth/pages/login/login";
import { RegisterCustomer } from "./auth/pages/register-customer/register-customer";
import { RegisterDriver } from "./auth/pages/register-driver/register-driver";

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Login, RegisterCustomer, RegisterDriver],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly title = signal('shipment-tracking-frontend');
}
