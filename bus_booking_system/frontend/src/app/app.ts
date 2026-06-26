import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './services/auth.service';
import { LoginResponse } from './models/auth.models';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-root',
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  currentUser$: Observable<LoginResponse | null>;
  dropdownOpen = false;

  constructor(private readonly authService: AuthService) {
    this.currentUser$ = this.authService.currentUser$;
  }

  /** Close dropdown when user clicks anywhere outside the profile button area. */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('#btn-profile-avatar') && !target.closest('#profile-dropdown')) {
      this.dropdownOpen = false;
    }
  }

  toggleDropdown(event: MouseEvent): void {
    event.stopPropagation();
    this.dropdownOpen = !this.dropdownOpen;
  }

  logout(): void {
    this.dropdownOpen = false;
    this.authService.logout();
  }

  /** Returns the first letter of the user's name for the avatar circle. */
  getInitial(name: string | undefined | null): string {
    return name?.trim().charAt(0).toUpperCase() ?? '?';
  }
}
