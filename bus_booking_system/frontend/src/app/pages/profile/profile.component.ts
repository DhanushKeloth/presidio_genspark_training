import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="min-h-screen bg-slate-100 p-6">
      <div class="mx-auto max-w-2xl rounded-xl bg-white p-6 shadow-sm">
        <h1 class="text-2xl font-bold text-slate-900">Profile</h1>

        <form class="mt-6 space-y-3" [formGroup]="form" (ngSubmit)="save()">
          <input class="w-full rounded-lg border border-slate-300 px-3 py-2" formControlName="name" placeholder="Name" />
          <input class="w-full rounded-lg border border-slate-300 px-3 py-2" formControlName="email" placeholder="Email" />
          <input class="w-full rounded-lg border border-slate-300 px-3 py-2" formControlName="phone" placeholder="Phone" />
          <button class="rounded-lg bg-blue-600 px-4 py-2 font-semibold text-white" type="submit">Save Profile</button>
        </form>
      </div>
    </div>
  `
})
export class ProfileComponent {
  readonly form: FormGroup;

  constructor(private readonly fb: FormBuilder) {
    this.form = this.fb.group({
      name: ['Rahul Verma', Validators.required],
      email: ['rahul.verma@example.com', [Validators.required, Validators.email]],
      phone: ['9876543210', Validators.required]
    });
  }

  save(): void {
    if (this.form.valid) {
      window.alert('Profile saved locally (mock).');
    }
  }
}
