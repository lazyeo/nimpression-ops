import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { AuthSuccessResponse } from '../models/auth.models';
import { I18nService } from '../i18n/i18n.service';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let routerMock: { navigate: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    TestBed.resetTestingModule();
    if (typeof localStorage !== 'undefined') {
      try {
        localStorage.clear();
      } catch {
        /* ignore */
      }
    }
    routerMock = { navigate: vi.fn() };

    TestBed.configureTestingModule({
      providers: [
        AuthService,
        I18nService,
        { provide: Router, useValue: routerMock },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    if (typeof localStorage !== 'undefined') {
      try {
        localStorage.clear();
      } catch {
        /* ignore */
      }
    }
  });

  it('handles login and establishes authenticated state with role signals', () => {
    const mockResponse: AuthSuccessResponse = {
      accessToken: 'dev-only-insecure-test-jwt-token',
      expiresIn: 3600,
      tokenType: 'Bearer',
      user: {
        id: 'u-1',
        email: 'driver@nimpression.co.nz',
        displayName: 'John Driver',
        role: 'Driver',
        locale: 'en-NZ',
      },
    };

    service
      .login({ email: 'driver@nimpression.co.nz', password: 'dev-only-insecure-password-123!' })
      .subscribe((res) => {
        expect(res.accessToken).toBe('dev-only-insecure-test-jwt-token');
      });

    const req = httpMock.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    req.flush(mockResponse);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.userRole()).toBe('Driver');
    expect(service.isDriver()).toBe(true);
    expect(service.isAdmin()).toBe(false);
  });

  it('handles logout and clears storage and signals', () => {
    service.setSession({
      accessToken: 'dev-only-insecure-token-to-clear',
      expiresIn: 3600,
      tokenType: 'Bearer',
      user: {
        id: 'u-2',
        email: 'admin@nim.co.nz',
        displayName: 'Admin',
        role: 'Admin',
        locale: 'zh-CN',
      },
    });

    expect(service.isAuthenticated()).toBe(true);

    service.logout().subscribe();
    const req = httpMock.expectOne('/api/auth/logout');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(service.isAuthenticated()).toBe(false);
    expect(service.currentUser()).toBeNull();
    expect(routerMock.navigate).toHaveBeenCalledWith(['/auth/login']);
  });
});
