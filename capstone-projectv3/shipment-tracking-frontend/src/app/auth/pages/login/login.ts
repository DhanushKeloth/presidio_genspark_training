import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { BehaviorSubject, EMPTY, catchError, finalize } from 'rxjs';

import { LoginModel } from '../../../shared/models/Login.model';
import { AuthService } from '../../../core/services/auth/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule
  ],
  templateUrl: './login.html',
  styleUrl: './login.css',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Login {

  model = new LoginModel();

  showPassword = false;

  isLoading$ = new BehaviorSubject<boolean>(false);
  errorMessage$ = new BehaviorSubject<string>('');

  constructor(
    private authService: AuthService,
    private router: Router
  ) {}

  togglePassword() {
    this.showPassword = !this.showPassword;
  }

  onSubmit(form: NgForm) {

    if (form.invalid) {
      Object.keys(form.controls).forEach(key =>
        form.controls[key].markAsTouched()
      );
      return;
    }

    this.errorMessage$.next('');
    this.isLoading$.next(true);

    this.authService.loginApiCall(this.model)
      .pipe(
        catchError(err => {

          this.errorMessage$.next(
            err.error?.message ||
            'Invalid email or password.'
          );

          return EMPTY;
        }),
        finalize(() => {
          this.isLoading$.next(false);
        })
      )
      .subscribe(response => {

        this.authService.setToken(response.accessToken);

        alert(`${response.fullName} ${response.role}`)
        if (response.role === 'Admin') {
          this.router.navigate(['/admin']);
        }
        else if (response.role === 'Driver') {
          this.router.navigate(['/driver']);
        }
        else {
          this.router.navigate(['/customer']);
        }

      });
  }
}