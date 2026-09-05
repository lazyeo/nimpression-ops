import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../auth/auth.service';
import { I18nService } from '../i18n/i18n.service';

describe('AuthInterceptor (F13.3 Accept-Language & Token Refresh)', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let authService: AuthService;
  let i18nService: I18nService;
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
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    authService = TestBed.inject(AuthService);
    i18nService = TestBed.inject(I18nService);
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

  it('attaches Accept-Language header matching current language (AC: F13.3)', () => {
    i18nService.setLanguage('zh-CN');
    httpClient.get('/api/test-locale').subscribe();

    const reqZh = httpMock.expectOne('/api/test-locale');
    expect(reqZh.request.headers.get('Accept-Language')).toContain('zh-CN');
    reqZh.flush({ ok: true });

    i18nService.setLanguage('en-NZ');
    httpClient.get('/api/test-locale-en').subscribe();

    const reqEn = httpMock.expectOne('/api/test-locale-en');
    expect(reqEn.request.headers.get('Accept-Language')).toContain('en-NZ');
    reqEn.flush({ ok: true });
  });

  it('attaches Authorization header when user is authenticated', () => {
    authService.setSession({
      accessToken: 'dev-only-insecure-bearer-token',
      expiresIn: 3600,
      tokenType: 'Bearer',
      user: {
        id: 'u-1',
        email: 'test@nim.co.nz',
        displayName: 'Test',
        role: 'Driver',
        locale: 'en-NZ',
      },
    });

    httpClient.get('/api/secure-data').subscribe();
    const req = httpMock.expectOne('/api/secure-data');
    expect(req.request.headers.get('Authorization')).toBe('Bearer dev-only-insecure-bearer-token');
    req.flush({ data: 123 });
  });

  it('handles 401 by attempting token refresh and retrying original request', () => {
    authService.setSession({
      accessToken: 'dev-only-insecure-expired-token',
      expiresIn: 3600,
      tokenType: 'Bearer',
      user: {
        id: 'u-1',
        email: 'test@nim.co.nz',
        displayName: 'Test',
        role: 'Admin',
        locale: 'en-NZ',
      },
    });

    httpClient.get('/api/protected-resource').subscribe((res: any) => {
      expect(res.success).toBe(true);
    });

    // 1. Initial request fails with 401
    const req1 = httpMock.expectOne('/api/protected-resource');
    req1.flush({ error: 'unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    // 2. Interceptor triggers /api/auth/refresh
    const refreshReq = httpMock.expectOne('/api/auth/refresh');
    refreshReq.flush({
      accessToken: 'dev-only-insecure-new-refreshed-token',
      expiresIn: 3600,
      tokenType: 'Bearer',
      user: {
        id: 'u-1',
        email: 'test@nim.co.nz',
        displayName: 'Test',
        role: 'Admin',
        locale: 'en-NZ',
      },
    });

    // 3. Original request retried with new token
    const retryReq = httpMock.expectOne('/api/protected-resource');
    expect(retryReq.request.headers.get('Authorization')).toBe('Bearer dev-only-insecure-new-refreshed-token');
    retryReq.flush({ success: true });

    expect(authService.accessToken()).toBe('dev-only-insecure-new-refreshed-token');
  });
});
