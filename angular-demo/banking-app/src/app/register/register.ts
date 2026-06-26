import { Component, signal } from '@angular/core';
import { BankingApiService } from '../services/bankingapi.service';
import { RegisterModel } from '../models/register.mode';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-register',
  imports: [FormsModule],
  templateUrl: './register.html',
  styleUrl: './register.css',
})
export class Register {
  isLoading=signal(false);
  registerModel = signal(new RegisterModel());
  constructor(private bankingApiService:BankingApiService){

  }
  handleRegisterClick(){
    console.log("register button clicked");
    this.isLoading.set(true);
    this.bankingApiService.registerApiCall(this.registerModel()).subscribe({
      next: (response) => {
        console.log("register successful", response);
        alert("register successful!")
        this.isLoading.set(false);
      },
      error: (error) => {
        console.error("register failed", error);
        alert("register failed. Please try again.");
        this.isLoading.set(false);
      }
    });
  }
}
