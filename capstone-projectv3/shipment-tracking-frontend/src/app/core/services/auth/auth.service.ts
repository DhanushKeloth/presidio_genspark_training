import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';

// Import the environment file to handle your URLs dynamically
import { environment } from '../../../../environments/environment';

// Your separated model imports
import { AuthResponseModel } from '../../../shared/models/AuthReponse.model';
import { LoginModel } from '../../../shared/models/Login.model';
import { RegisterCustomerModel } from '../../../shared/models/RegisterCustomer.model';
import { RegisterDriverModel } from '../../../shared/models/RegisterDriver.model';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  // Matches the exact key your interceptor is looking for
  private readonly TOKEN_KEY = 'token'; 

  constructor(private http: HttpClient, private router: Router) {}

  

  public loginApiCall(credentials: LoginModel) {
    let url = environment.baseUrl + '/Auth/login';
    return this.http.post<AuthResponseModel>(url, credentials);
    
  }

  public registerCustomerApiCall(data: RegisterCustomerModel) {
    let url = environment.baseUrl + '/Auth/register/customer';
    return this.http.post(url, data);
  }

  public registerDriverApiCall(data: RegisterDriverModel) {
    let url = environment.baseUrl + '/Auth/register/driver';
    return this.http.post(url, data);
  }

  // --- TOKEN MANAGEMENT ---

  public setToken(token: string): void {
    sessionStorage.setItem(this.TOKEN_KEY, token);
  }

  public getToken(): string | null {
    return sessionStorage.getItem(this.TOKEN_KEY);
  }

  public logout(): void {
    sessionStorage.removeItem(this.TOKEN_KEY);
    this.router.navigate(['/auth/login']);
  }

  public isLoggedIn(): boolean {
    const token = this.getToken();
    return !!token; 
  }

  // --- JWT DECODING (ASP.NET Core Specific) ---
  
  public getRole(): string | null {
    const token = this.getToken();
    if (!token) return null;

    try {
      // Decode the middle part (payload) of the JWT
      const payloadBase64 = token.split('.')[1];
      const decodedJson = atob(payloadBase64);
      const decodedToken = JSON.parse(decodedJson);

      // ASP.NET Core often maps Roles to this specific XML schema URL by default
      const aspNetRoleClaim = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
      
      // Return the Microsoft claim, or fall back to a standard 'role' property
      return decodedToken[aspNetRoleClaim] || decodedToken.role || null;
      
    } catch (error) {
      console.error('Error decoding token:', error);
      return null;
    }
  }
}