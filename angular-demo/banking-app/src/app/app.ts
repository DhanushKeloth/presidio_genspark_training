import { Component, inject, signal } from '@angular/core';
import { Event,NavigationCancel, NavigationEnd, NavigationError, NavigationStart, Router, RouterOutlet } from '@angular/router';
import { Customers } from './customers/customers';
import { Products } from "./products/products";
import { Login } from "./login/login";
import { Register } from './register/register';
import { Child } from "./child/child";
import { Parent } from "./parent/parent";
import { NavbarComponent } from './navbar-component/navbar-component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet,NavbarComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly title = signal('banking-app');

  private router = inject(Router);
  isLoading = false;

  constructor() {
    // Listen to the stream of router events
    this.router.events.subscribe((event: Event) => {
      if (event instanceof NavigationStart) {
        // Navigation started -> Turn spinner ON
        this.isLoading = true;
      } else if (
        event instanceof NavigationEnd || 
        event instanceof NavigationCancel || 
        event instanceof NavigationError
      ) {
        // Navigation finished/failed -> Turn spinner OFF
        this.isLoading = false;
      }
    });
  }
}
