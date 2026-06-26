import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from '../../services/auth.service';
import { ApiError } from '../../models/auth.models';

@Component({
  selector: 'app-auth',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="min-h-[calc(100vh-72px)] bg-slate-100">
      <div class="mx-auto grid max-w-7xl gap-8 px-4 py-10 lg:grid-cols-[1fr_1.1fr]">

        <!-- Left Panel -->
        <section class="flex flex-col justify-between rounded-2xl bg-gradient-to-b from-slate-100 to-white p-8">
          <div>
            <h1 class="text-5xl font-bold leading-tight text-slate-900">Welcome Back!</h1>
            <p class="mt-3 text-4xl font-semibold leading-tight text-blue-800">
              Login or create your account<br />to book your journey.
            </p>

            <div class="mt-10 space-y-7">
              <div class="flex items-start gap-4">
                <div class="flex h-12 w-12 items-center justify-center rounded-full border border-blue-100 bg-white text-xl text-blue-700">☆</div>
                <div>
                  <p class="text-2xl font-semibold text-slate-900">Easy Bookings</p>
                  <p class="text-lg text-slate-600">Book bus tickets in just a few clicks</p>
                </div>
              </div>
              <div class="flex items-start gap-4">
                <div class="flex h-12 w-12 items-center justify-center rounded-full border border-blue-100 bg-white text-xl text-blue-700">🛡</div>
                <div>
                  <p class="text-2xl font-semibold text-slate-900">Safe &amp; Secure</p>
                  <p class="text-lg text-slate-600">Your data and payments are fully protected</p>
                </div>
              </div>
              <div class="flex items-start gap-4">
                <div class="flex h-12 w-12 items-center justify-center rounded-full border border-blue-100 bg-white text-xl text-blue-700">🪑</div>
                <div>
                  <p class="text-2xl font-semibold text-slate-900">Wide Choices</p>
                  <p class="text-lg text-slate-600">Choose from a wide range of buses and routes</p>
                </div>
              </div>
            </div>
          </div>

          <div class="mt-8 rounded-2xl border-2 border-dashed border-slate-300 bg-white p-5 text-center text-slate-500">
            🚌 Book your seat — travel smarter.
          </div>
        </section>

        <!-- Right Panel: Form -->
        <section class="rounded-2xl bg-white shadow-sm">
          <!-- Tab bar -->
          <div class="grid grid-cols-2 border-b border-slate-200">
            <button
              id="tab-login"
              class="p-4 text-lg font-semibold"
              [ngClass]="isLogin ? 'border-b-2 border-blue-600 text-blue-700' : 'text-slate-500'"
              (click)="switchMode(true)">
              Login
            </button>
            <button
              id="tab-register"
              class="p-4 text-lg font-semibold"
              [ngClass]="!isLogin ? 'border-b-2 border-blue-600 text-blue-700' : 'text-slate-500'"
              (click)="switchMode(false)">
              Register
            </button>
          </div>

          <div class="p-8">
            <h2 class="text-3xl font-semibold text-slate-900">
              {{ isLogin ? 'Login to your account' : 'Create your account' }}
            </h2>
            <p class="mt-2 text-lg text-slate-500">
              {{ isLogin ? 'Enter your credentials to continue' : 'Sign up to start booking your journeys' }}
            </p>

            <!-- API success banner -->
            <div
              *ngIf="successMessage"
              class="mt-4 rounded-lg border border-green-300 bg-green-50 px-4 py-3 text-sm text-green-700">
              {{ successMessage }}
            </div>

            <!-- API error banner -->
            <div
              *ngIf="apiError"
              class="mt-4 rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-700">
              {{ apiError }}
            </div>

            <form class="mt-6 space-y-5" [formGroup]="authForm" (ngSubmit)="submit()">

              <!-- Full Name (register only) -->
              <div *ngIf="!isLogin">
                <input
                  id="input-fullname"
                  class="w-full rounded-lg border px-4 py-4 text-lg outline-none transition"
                  [ngClass]="fieldError('fullName') ? 'border-red-400 bg-red-50' : 'border-slate-300 focus:border-blue-500'"
                  placeholder="Enter your full name"
                  formControlName="fullName" />
                <p *ngIf="fieldError('fullName')" class="mt-1 text-sm text-red-600">Full name is required.</p>
              </div>

              <!-- Email -->
              <div>
                <input
                  id="input-email"
                  class="w-full rounded-lg border px-4 py-4 text-lg outline-none transition"
                  [ngClass]="fieldError('email') ? 'border-red-400 bg-red-50' : 'border-slate-300 focus:border-blue-500'"
                  placeholder="Enter your email address"
                  formControlName="email" />
                <p *ngIf="fieldError('email')" class="mt-1 text-sm text-red-600">A valid email address is required.</p>
              </div>

              <!-- Phone (register only) -->
              <div *ngIf="!isLogin">
                <input
                  id="input-phone"
                  class="w-full rounded-lg border border-slate-300 px-4 py-4 text-lg outline-none transition focus:border-blue-500"
                  placeholder="Enter your mobile number (optional)"
                  formControlName="phone" />
              </div>

              <!-- Password -->
              <div>
                <input
                  id="input-password"
                  class="w-full rounded-lg border px-4 py-4 text-lg outline-none transition"
                  [ngClass]="fieldError('password') ? 'border-red-400 bg-red-50' : 'border-slate-300 focus:border-blue-500'"
                  type="password"
                  placeholder="Enter your password"
                  formControlName="password" />
                <p *ngIf="fieldError('password')" class="mt-1 text-sm text-red-600">Password is required.</p>
              </div>

              <!-- Confirm Password (register only) -->
              <div *ngIf="!isLogin">
                <input
                  id="input-confirm-password"
                  class="w-full rounded-lg border px-4 py-4 text-lg outline-none transition"
                  [ngClass]="passwordMismatch ? 'border-red-400 bg-red-50' : 'border-slate-300 focus:border-blue-500'"
                  type="password"
                  placeholder="Confirm your password"
                  formControlName="confirmPassword" />
                <p *ngIf="passwordMismatch" class="mt-1 text-sm text-red-600">
                  Passwords do not match.
                </p>
              </div>

              <!-- Submit -->
              <button
                id="btn-submit"
                type="submit"
                class="w-full rounded-lg px-5 py-4 text-xl font-semibold text-white transition"
                [ngClass]="isLoading ? 'bg-blue-400 cursor-not-allowed' : 'bg-blue-600 hover:bg-blue-700 active:bg-blue-800'"
                [disabled]="isLoading">
                <span *ngIf="!isLoading">{{ isLogin ? 'Login' : 'Register' }}</span>
                <span *ngIf="isLoading" class="flex items-center justify-center gap-2">
                  <svg class="h-5 w-5 animate-spin" viewBox="0 0 24 24" fill="none">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z"/>
                  </svg>
                  {{ isLogin ? 'Logging in…' : 'Creating account…' }}
                </span>
              </button>
            </form>

            <p class="mt-6 text-center text-slate-600">
              {{ isLogin ? "Don't have an account?" : 'Already have an account?' }}
              <button
                id="btn-toggle-mode"
                class="ml-1 font-semibold text-blue-700 hover:underline"
                (click)="switchMode(!isLogin)">
                {{ isLogin ? 'Register now' : 'Login now' }}
              </button>
            </p>
          </div>
        </section>
      </div>
    </div>
  `
})
export class AuthComponent {
  isLogin = true;
  isLoading = false;
  passwordMismatch = false;
  apiError: string | null = null;
  successMessage: string | null = null;

  authForm: FormGroup;

  constructor(
    private readonly fb: FormBuilder,
    private readonly authService: AuthService,
    private readonly router: Router
  ) {
    this.authForm = this.buildForm();
  }

  // ── Mode toggle ───────────────────────────────────────────────────────────
  switchMode(loginMode: boolean): void {
    this.isLogin = loginMode;
    this.passwordMismatch = false;
    this.apiError = null;
    this.successMessage = null;
    this.authForm = this.buildForm();
  }

  // ── Field-level error helper ──────────────────────────────────────────────
  fieldError(name: string): boolean {
    const control = this.authForm.get(name);
    return !!(control?.invalid && control?.touched);
  }

  // ── Submit ────────────────────────────────────────────────────────────────
  submit(): void {
    this.apiError = null;
    this.successMessage = null;
    this.authForm.markAllAsTouched();

    if (this.authForm.invalid) return;

    if (!this.isLogin) {
      const pw = this.authForm.value.password as string;
      const cpw = this.authForm.value.confirmPassword as string;
      this.passwordMismatch = pw !== cpw;
      if (this.passwordMismatch) return;
      this.registerUser();
    } else {
      this.loginUser();
    }
  }

  // ── Login ─────────────────────────────────────────────────────────────────
  private loginUser(): void {
    this.isLoading = true;
    this.authService
      .login({ email: this.authForm.value.email, password: this.authForm.value.password })
      .subscribe({
        next: () => {
          this.isLoading = false;
          this.router.navigate(['/user/search']);
        },
        error: (err: HttpErrorResponse) => {
          this.isLoading = false;
          this.apiError = this.extractMessage(err);
        }
      });
  }

  // ── Register ──────────────────────────────────────────────────────────────
  private registerUser(): void {
    this.isLoading = true;
    const { fullName, email, phone, password, confirmPassword } = this.authForm.value as {
      fullName: string;
      email: string;
      phone: string;
      password: string;
      confirmPassword: string;
    };

    this.authService
      .register({ fullName, email, phone: phone || undefined, password, confirmPassword })
      .subscribe({
        next: (res) => {
          this.isLoading = false;
          this.successMessage = res.message ?? 'Account created! Please log in.';
          this.switchMode(true);
        },
        error: (err: HttpErrorResponse) => {
          this.isLoading = false;
          this.apiError = this.extractMessage(err);
        }
      });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────
  private buildForm(): FormGroup {
    return this.fb.group({
      fullName: [this.isLogin ? '' : '', this.isLogin ? [] : [Validators.required]],
      email: ['', [Validators.required, Validators.email]],
      phone: [''],
      password: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: [this.isLogin ? '' : '']
    });
  }

  private extractMessage(err: HttpErrorResponse): string {
    const body = err.error as ApiError | null;
    if (body?.message) return body.message;
    if (err.status === 0) return 'Cannot reach the server. Please check if the API is running.';
    return `Unexpected error (${err.status}). Please try again.`;
  }
}
