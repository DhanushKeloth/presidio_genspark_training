import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { LoginRequest, LoginResponse, UserRegisterRequest } from '../models/auth.models';

const API_BASE = 'http://localhost:5111/api/v1';
const TOKEN_KEY = 'auth_token';
const USER_KEY = 'auth_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly _currentUser$ = new BehaviorSubject<LoginResponse | null>(
    this.loadUser()
  );

  readonly currentUser$ = this._currentUser$.asObservable();

  constructor(private readonly http: HttpClient) {}

  // ── Register ──────────────────────────────────────────────────────────────
  register(payload: UserRegisterRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${API_BASE}/auth/register`,
      payload
    );
  }

  // ── Login ─────────────────────────────────────────────────────────────────
  login(payload: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${API_BASE}/auth/login`, payload).pipe(
      tap((response) => this.persistSession(response))
    );
  }

  // ── Logout ────────────────────────────────────────────────────────────────
  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this._currentUser$.next(null);
  }

  // ── Helpers ───────────────────────────────────────────────────────────────
  isLoggedIn(): boolean {
    return !!this._currentUser$.value;
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  private persistSession(response: LoginResponse): void {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(USER_KEY, JSON.stringify(response));
    this._currentUser$.next(response);
  }

  private loadUser(): LoginResponse | null {
    try {
      const raw = localStorage.getItem(USER_KEY);
      if (!raw) return null;
      const parsed = JSON.parse(raw) as LoginResponse;
      // Discard sessions stored before the `name` field was added
      if (!parsed?.name) {
        localStorage.removeItem(TOKEN_KEY);
        localStorage.removeItem(USER_KEY);
        return null;
      }
      return parsed;
    } catch {
      return null;
    }
  }
}
