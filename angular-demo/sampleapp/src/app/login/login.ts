import { Component, signal } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { LoginModel } from '../models/login.model';
import { AuthService } from '../services/auth.service';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-login',
  imports: [FormsModule,CommonModule,ReactiveFormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  // userName:string = "dhanushkeloth@mail.com";
  // password:string ="1234";

  loginModel = new LoginModel();

  constructor(
    private authService:AuthService,
    private router:Router,
  
  ){
   

  }

  onSubmit(){
    

    if (!this.loginModel.username || !this.loginModel.password) {
      return;
    }
    this.authService.login(this.loginModel).subscribe({
      next:(user)=>{
        alert("login successful")
        this.router.navigate(['/dashboard'])
        // console.log('login successful ');
        // console.log(user.firstName);
        // console.log(user);
      },
      error:(err)=>{
        alert("login failed")
        console.error('login failed');
      }
    })
  }
 
}
