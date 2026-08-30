import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, map, Observable, of, tap, throwError } from 'rxjs';
import { AuthSuccessResponse, AuthUser, LoginRequest, UserRole } from '../models/auth.models';
import { I18nService } from '../i18n/i18n.service';
import { SupportedLang } from '../models/i18n.models';

const STORAGE_AUTH_TOKEN = 'nim_auth_token';
const STORAGE_AUTH_USER = 'nim_auth_user';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly i18n = inject(I18nService);

  readonly currentUser = signal<AuthUser | null>(this.getStoredUser());
  readonly accessToken = signal<string | null>(this.getStoredToken());

  readonly isAuthenticated = computed(() => !!this.accessToken() && !!this.currentUser());
  readonly userRole = computed<UserRole | null>(() => this.currentUser()?.role ?? null);
  readonly isAdmin = computed(() => this.userRole() === 'Admin');
  readonly isDispatcher = computed(() => this.userRole() === 'Dispatcher');
  readonly isDriver = computed(() => this.userRole() === 'Driver');
  readonly isAdminOrDispatcher = computed(() => this.isAdmin() || this.isDispatcher());

  private getStoredToken(): string | null {
    try {
      return localStorage.getItem(STORAGE_AUTH_TOKEN);
    } catch {
      return null;
    }
  }

  private getStoredUser(): AuthUser | null {
    try {
      const raw = localStorage.getItem(STORAGE_AUTH_USER);
      return raw ? (JSON.parse(raw) as AuthUser) : null;
    } catch {
      return null;
    }
  }

  login(credentials: LoginRequest): Observable<AuthSuccessResponse> {
    return this.http.post<AuthSuccessResponse>('/api/auth/login', credentials).pipe(
      tap((res) => {
        this.setSession(res);
      }),
    );
  }

  refreshToken(): Observable<AuthSuccessResponse> {
    return this.http.post<AuthSuccessResponse>('/api/auth/refresh', {}).pipe(
      tap((res) => {
        this.setSession(res);
      }),
      catchError((err) => {
        this.clearSession();
        return throwError(() => err);
      }),
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', {}).pipe(
      catchError(() => of(undefined as void)),
      tap(() => {
        this.clearSession();
        void this.router.navigate(['/auth/login']);
      }),
    );
  }

  setSession(auth: AuthSuccessResponse): void {
    this.accessToken.set(auth.accessToken);
    this.currentUser.set(auth.user);

    try {
      localStorage.setItem(STORAGE_AUTH_TOKEN, auth.accessToken);
      localStorage.setItem(STORAGE_AUTH_USER, JSON.stringify(auth.user));
    } catch {
      // Ignore storage errors
    }

    if (auth.user?.locale === 'zh-CN' || auth.user?.locale === 'en-NZ') {
      void this.i18n.setLanguage(auth.user.locale as SupportedLang);
    }
  }

  clearSession(): void {
    this.accessToken.set(null);
    this.currentUser.set(null);

    try {
      localStorage.removeItem(STORAGE_AUTH_TOKEN);
      localStorage.removeItem(STORAGE_AUTH_USER);
    } catch {
      // Ignore storage errors
    }
  }

  updateUserLocale(locale: SupportedLang): Observable<void> {
    const user = this.currentUser();
    if (!user) return of(undefined);

    const updatedUser: AuthUser = { ...user, locale };
    this.currentUser.set(updatedUser);
    try {
      localStorage.setItem(STORAGE_AUTH_USER, JSON.stringify(updatedUser));
    } catch {
      // Ignore
    }

    void this.i18n.setLanguage(locale);

    // If driver, sync with driver profile endpoint
    if (user.role === 'Driver') {
      return this.http
        .put<void>(`/api/drivers/${user.id}/profile`, { locale })
        .pipe(catchError(() => of(undefined)));
    }

    return of(undefined);
  }
}
