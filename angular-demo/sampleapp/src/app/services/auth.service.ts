import { HttpClient } from '@angular/common/http';
import { LoginModel } from '../models/login.model';
import { Injectable } from '@angular/core';
import { BehaviorSubject, pipe, tap } from 'rxjs';
import { User } from '../models/user.model';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private userSubject = new BehaviorSubject<User | null>(null);
  public user$ = this.userSubject.asObservable();
  constructor(private http: HttpClient) {
    const savedUser = sessionStorage.getItem('user');
    if(savedUser){
        this.userSubject.next(JSON.parse(savedUser))
    }
  }
  public login(loginModel: LoginModel) {
    let loginUrl = 'https://dummyjson.com/auth/login';

    return this.http.post<User>(loginUrl, loginModel).pipe(
      tap((userData: User) => {
        this.userSubject.next(userData);
        
        // sessionStorage.setItem('token', userData.token);
        // sessionStorage.setItem('firstName', userData.firstName);
        sessionStorage.setItem('user',JSON.stringify(userData))
      }),
    );
  }
  public logout(): void {
    sessionStorage.clear();
    this.userSubject.next(null);
  }
  isLoggedIn(): boolean {
    return this.userSubject.value !== null;
  }
}
