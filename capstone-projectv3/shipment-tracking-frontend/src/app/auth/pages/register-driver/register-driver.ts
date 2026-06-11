import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { BehaviorSubject, EMPTY, catchError, finalize } from 'rxjs';

import { RegisterDriverModel } from '../../../shared/models/RegisterDriver.model';
import { AuthService } from '../../../core/services/auth/auth.service';

@Component({
  selector: 'app-register-driver',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule
  ],
  templateUrl: './register-driver.html',
  styleUrl: './register-driver.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RegisterDriver {

  model = new RegisterDriverModel();

  showPassword = false;

  isLoading$ = new BehaviorSubject<boolean>(false);
  errorMessage$ = new BehaviorSubject<string>('');

  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  togglePassword(): void {
    this.showPassword = !this.showPassword;
  }

  onSubmit(form: NgForm): void {

    if (form.invalid) {
      Object.keys(form.controls).forEach(key => {
        form.controls[key].markAsTouched();
      });
      return;
    }

    if (this.model.password !== this.model.confirmPassword) {
      this.errorMessage$.next('Passwords do not match.');
      return;
    }

    this.errorMessage$.next('');
    this.isLoading$.next(true);

    this.authService.registerDriverApiCall(this.model)
      .pipe(
        catchError(err => {

          if (err.error?.errors) {

            const firstErrorKey =
              Object.keys(err.error.errors)[0];

            this.errorMessage$.next(
              err.error.errors[firstErrorKey][0]
            );

          } else {

            this.errorMessage$.next(
              err.error?.message ||
              'Driver registration failed.'
            );

          }

          return EMPTY;
        }),
        finalize(() => {
          this.isLoading$.next(false);
        })
      )
      .subscribe(() => {

        alert(
          `Driver ${this.model.fullName} registered successfully`
        );

        this.router.navigate(['/auth/login']);

      });
  }
}