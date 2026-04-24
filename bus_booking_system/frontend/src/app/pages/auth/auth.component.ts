import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-auth',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  template: `
    <div class="min-h-[calc(100vh-72px)] bg-slate-100">
      <div class="mx-auto grid max-w-7xl gap-8 px-4 py-10 lg:grid-cols-[1fr_1.1fr]">
        <section class="flex flex-col justify-between rounded-2xl bg-gradient-to-b from-slate-100 to-white p-8">
          <div>
            <h1 class="text-5xl font-bold leading-tight text-slate-900">Welcome Back!</h1>
            <p class="mt-3 text-4xl font-semibold leading-tight text-blue-800">Login or create your account<br />to book your journey.</p>

            <div class="mt-10 space-y-7">
              <div class="flex items-start gap-4">
                <div class="flex h-12 w-12 items-center justify-center rounded-full border border-blue-100 bg-white text-xl text-blue-700">☆</div>
                <div><p class="text-2xl font-semibold text-slate-900">Easy Bookings</p><p class="text-lg text-slate-600">Book bus tickets in just a few clicks</p></div>
              </div>
              <div class="flex items-start gap-4">
                <div class="flex h-12 w-12 items-center justify-center rounded-full border border-blue-100 bg-white text-xl text-blue-700">🛡</div>
                <div><p class="text-2xl font-semibold text-slate-900">Safe & Secure</p><p class="text-lg text-slate-600">Your data and payments are fully protected</p></div>
              </div>
              <div class="flex items-start gap-4">
                <div class="flex h-12 w-12 items-center justify-center rounded-full border border-blue-100 bg-white text-xl text-blue-700">🪑</div>
                <div><p class="text-2xl font-semibold text-slate-900">Wide Choices</p><p class="text-lg text-slate-600">Choose from a wide range of buses and routes</p></div>
              </div>
            </div>
          </div>

          <div class="mt-8 rounded-2xl border-2 border-dashed border-slate-300 bg-white p-5 text-center text-slate-500">
            Bus Image Placeholder
          </div>
        </section>

        <section class="rounded-2xl bg-white shadow-sm">
          <div class="grid grid-cols-2 border-b border-slate-200">
            <button class="p-4 text-lg font-semibold" [ngClass]="isLogin ? 'border-b-2 border-blue-600 text-blue-700' : 'text-slate-500'" (click)="isLogin = true">Login</button>
            <button class="p-4 text-lg font-semibold" [ngClass]="!isLogin ? 'border-b-2 border-blue-600 text-blue-700' : 'text-slate-500'" (click)="isLogin = false">Register</button>
          </div>

          <div class="p-8">
            <h2 class="text-3xl font-semibold text-slate-900">{{ isLogin ? 'Login to your account' : 'Create your account' }}</h2>
            <p class="mt-2 text-lg text-slate-500">{{ isLogin ? 'Enter your credentials to continue' : 'Sign up to start booking your journeys' }}</p>

            <form class="mt-8 space-y-5" [formGroup]="authForm" (ngSubmit)="submit()">
              <input class="w-full rounded-lg border border-slate-300 px-4 py-4 text-lg" [placeholder]="isLogin ? 'Enter your email or phone number' : 'Enter your full name'" formControlName="primary" />
              <input *ngIf="!isLogin" class="w-full rounded-lg border border-slate-300 px-4 py-4 text-lg" placeholder="Enter your email address" formControlName="email" />
              <input *ngIf="!isLogin" class="w-full rounded-lg border border-slate-300 px-4 py-4 text-lg" placeholder="Enter your mobile number" formControlName="mobile" />
              <input class="w-full rounded-lg border border-slate-300 px-4 py-4 text-lg" type="password" placeholder="Enter your password" formControlName="password" />
              <input *ngIf="!isLogin" class="w-full rounded-lg border border-slate-300 px-4 py-4 text-lg" type="password" placeholder="Confirm your password" formControlName="confirmPassword" />
              <p *ngIf="!isLogin && passwordMismatch" class="text-sm font-medium text-red-600">Password and confirm password must match.</p>
              <button class="w-full rounded-lg bg-blue-600 px-5 py-4 text-xl font-semibold text-white">{{ isLogin ? 'Login' : 'Register' }}</button>
            </form>

            <div class="my-6 text-center text-slate-400">or</div>
            <button class="w-full rounded-lg border border-slate-300 px-5 py-4 text-lg font-medium text-slate-700">Continue with Google</button>
            <p class="mt-6 text-center text-slate-600">
              {{ isLogin ? "Don't have an account?" : "Already have an account?" }}
              <button class="font-semibold text-blue-700" (click)="toggleMode()">{{ isLogin ? 'Register now' : 'Login now' }}</button>
            </p>
          </div>
        </section>
      </div>
    </div>
  `
})
export class AuthComponent {
  isLogin = true;
  passwordMismatch = false;
  authForm: FormGroup;

  constructor(private readonly fb: FormBuilder) {
    this.authForm = this.fb.group({
      primary: ['', Validators.required],
      email: [''],
      mobile: [''],
      password: ['', Validators.required],
      confirmPassword: ['']
    });
  }

  toggleMode(): void {
    this.isLogin = !this.isLogin;
    this.passwordMismatch = false;
  }

  submit(): void {
    if (this.authForm.invalid) {
      this.authForm.markAllAsTouched();
      return;
    }

    if (!this.isLogin) {
      const password = this.authForm.get('password')?.value;
      const confirmPassword = this.authForm.get('confirmPassword')?.value;
      this.passwordMismatch = password !== confirmPassword;
      if (this.passwordMismatch) {
        return;
      }
    }
  }
}
