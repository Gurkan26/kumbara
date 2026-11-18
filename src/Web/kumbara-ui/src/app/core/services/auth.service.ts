import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, catchError, of, map, throwError } from 'rxjs';
import { ApiResponse, AuthResponse, LoginRequest, RegisterRequest, User } from '../models/auth.model';

const TOKEN_KEY = 'kumbara_access_token';
const REFRESH_TOKEN_KEY = 'kumbara_refresh_token';
const USER_KEY = 'kumbara_user';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private apiUrl = '/api/auth';

  readonly token = signal<string | null>(this.getStoredToken());
  readonly refreshTokenValue = signal<string | null>(this.getStoredRefreshToken());
  readonly currentUser = signal<User | null>(this.getStoredUser());
  readonly isAuthenticated = computed(() => !!this.token());

  constructor() {
    // If token exists but user does not, attempt decoding
    if (this.token() && !this.currentUser()) {
      const user = this.decodeToken(this.token()!);
      if (user) {
        this.currentUser.set(user);
        localStorage.setItem(USER_KEY, JSON.stringify(user));
      }
    }
  }

  private getStoredToken(): string | null {
    if (typeof window === 'undefined') return null;
    return localStorage.getItem(TOKEN_KEY) || sessionStorage.getItem(TOKEN_KEY);
  }

  private getStoredRefreshToken(): string | null {
    if (typeof window === 'undefined') return null;
    return localStorage.getItem(REFRESH_TOKEN_KEY) || sessionStorage.getItem(REFRESH_TOKEN_KEY);
  }

  private getStoredUser(): User | null {
    if (typeof window === 'undefined') return null;
    const raw = localStorage.getItem(USER_KEY);
    return raw ? JSON.parse(raw) : null;
  }

  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http.post<ApiResponse<AuthResponse>>(`${this.apiUrl}/login`, credentials).pipe(
      map(res => {
        if (!res.success || !res.data) {
          throw new Error(res.error || res.message || 'Giriş başarısız oldu.');
        }
        return res.data;
      }),
      tap(authData => this.handleAuthSuccess(authData))
    );
  }

  register(data: RegisterRequest): Observable<any> {
    return this.http.post<ApiResponse<any>>(`${this.apiUrl}/register`, data).pipe(
      map(res => {
        if (!res.success) {
          throw new Error(res.error || res.message || 'Kayıt başarısız oldu.');
        }
        return res.data;
      })
    );
  }

  refreshToken(): Observable<AuthResponse | null> {
    const currentRefreshToken = this.refreshTokenValue();
    if (!currentRefreshToken) {
      this.logout();
      return of(null);
    }

    return this.http.post<ApiResponse<AuthResponse>>(`${this.apiUrl}/refresh`, {
      refreshToken: currentRefreshToken
    }).pipe(
      map(res => {
        if (!res.success || !res.data) {
          throw new Error(res.error || 'Token yenileme başarısız');
        }
        return res.data;
      }),
      tap(authData => this.handleAuthSuccess(authData)),
      catchError(err => {
        this.logout();
        return throwError(() => err);
      })
    );
  }

  logout(): void {
    if (typeof window !== 'undefined') {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(REFRESH_TOKEN_KEY);
      localStorage.removeItem(USER_KEY);
      sessionStorage.removeItem(TOKEN_KEY);
      sessionStorage.removeItem(REFRESH_TOKEN_KEY);
    }
    this.token.set(null);
    this.refreshTokenValue.set(null);
    this.currentUser.set(null);
  }

  private handleAuthSuccess(authData: AuthResponse): void {
    const user = this.decodeToken(authData.accessToken) || {
      id: 'default-user',
      email: 'user@kumbara.local',
      fullName: 'Simülasyon Kullanıcısı'
    };

    if (typeof window !== 'undefined') {
      localStorage.setItem(TOKEN_KEY, authData.accessToken);
      localStorage.setItem(REFRESH_TOKEN_KEY, authData.refreshToken);
      localStorage.setItem(USER_KEY, JSON.stringify(user));
    }

    this.token.set(authData.accessToken);
    this.refreshTokenValue.set(authData.refreshToken);
    this.currentUser.set(user);
  }

  private decodeToken(token: string): User | null {
    try {
      const parts = token.split('.');
      if (parts.length !== 3) return null;
      const payload = JSON.parse(atob(parts[1].replace(/-/g, '+').replace(/_/g, '/')));
      const id = payload.sub || payload.nameid || payload.id ||
        payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ||
        payload['sub'] || '';
      const email = payload.email || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || '';
      const fullName = payload.fullName || payload.name || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] || (email ? email.split('@')[0] : 'Kullanıcı');
      return { id, email, fullName };
    } catch {
      return null;
    }
  }
}
