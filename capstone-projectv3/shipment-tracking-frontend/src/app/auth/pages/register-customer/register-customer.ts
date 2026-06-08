import { Component, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { BehaviorSubject, EMPTY, catchError, finalize } from 'rxjs';

import { RegisterCustomerModel } from '../../../shared/models/RegisterCustomer.model';
import { AuthService } from '../../../core/services/auth/auth.service';

@Component({
  selector: 'app-register-customer',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule], 
  templateUrl: './register-customer.html',
  styleUrl: './register-customer.css',
  changeDetection: ChangeDetectionStrategy.OnPush 
})
export class RegisterCustomer {
  model = new RegisterCustomerModel();
  
  isLoading$ = new BehaviorSubject<boolean>(false);
  errorMessage$ = new BehaviorSubject<string>('');
  showPassword = false;

  constructor(
    private authService: AuthService, 
    private router: Router
  ) {}

  togglePassword() {
    this.showPassword = !this.showPassword;
  }

  onSubmit(form: NgForm) {
    if (form.invalid) {
      Object.keys(form.controls).forEach(key => form.controls[key].markAsTouched());
      return;
    }

    if (this.model.password !== this.model.confirmPassword) {
      this.errorMessage$.next('Passwords do not match.');
      return;
    }

    this.isLoading$.next(true);
    this.errorMessage$.next('');

    // FIX applied here: Updated to match your new AuthService method name
    this.authService.registerCustomerApiCall(this.model).pipe(
      catchError(err => {
        if (err.error && err.error.errors) {
          const firstErrorKey = Object.keys(err.error.errors)[0];
          this.errorMessage$.next(err.error.errors[firstErrorKey][0]);
        } else {
          this.errorMessage$.next(err.error?.message || 'Registration failed. Please try again.');
        }
        return EMPTY; 
      }),
      finalize(() => {
        this.isLoading$.next(false);
      })
    ).subscribe(() => {
      this.router.navigate(['/auth/login']);
    });
  }
}