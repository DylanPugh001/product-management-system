import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { AuthState, LoginResponse, TOKEN_STORAGE_KEY, USER_STORAGE_KEY } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly stateSignal = signal<AuthState | null>(this.loadState());

  readonly state = this.stateSignal.asReadonly();

  get token(): string | null {
    return this.stateSignal()?.token ?? null;
  }

  get isAuthenticated(): boolean {
    return this.stateSignal() !== null;
  }

  get isManager(): boolean {
    return this.stateSignal()?.roles.includes('Manager') ?? false;
  }

  login(email: string, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>('/api/auth/login', { email, password })
      .pipe(tap((response) => this.setSession(response)));
  }

  logout(): void {
    this.stateSignal.set(null);
    localStorage.removeItem(TOKEN_STORAGE_KEY);
    localStorage.removeItem(USER_STORAGE_KEY);
    this.router.navigate(['/login']);
  }

  private setSession(response: LoginResponse): void {
    const state: AuthState = {
      token: response.token,
      userId: response.userId,
      email: response.email,
      roles: response.roles,
      expiresAt: response.expiresAt,
    };
    this.stateSignal.set(state);
    localStorage.setItem(TOKEN_STORAGE_KEY, response.token);
    localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(state));
  }

  private loadState(): AuthState | null {
    const raw = localStorage.getItem(USER_STORAGE_KEY);
    if (!raw) {
      return null;
    }
    try {
      return JSON.parse(raw) as AuthState;
    } catch {
      return null;
    }
  }
}
