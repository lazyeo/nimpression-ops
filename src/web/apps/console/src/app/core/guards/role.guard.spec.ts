import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, UrlTree } from '@angular/router';
import { roleGuard } from './role.guard';
import { AuthService } from '../auth/auth.service';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

describe('RoleGuard (Role routing & redirection)', () => {
  let authService: AuthService;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting()],
    });
    authService = TestBed.inject(AuthService);
  });

  it('redirects unauthenticated user to /auth/login', () => {
    authService.clearSession();
    const route = { data: { roles: ['Admin'] } } as unknown as ActivatedRouteSnapshot;
    const result = TestBed.runInInjectionContext(() => roleGuard(route, {} as any));

    expect(result instanceof UrlTree).toBe(true);
    expect((result as UrlTree).toString()).toBe('/auth/login');
  });

  it('redirects Driver cleanly to /driver when accessing admin routes (AC: Driver 访问 admin 路由要跳转而非报错)', () => {
    authService.setSession({
      accessToken: 'driver-token',
      expiresIn: 3600,
      tokenType: 'Bearer',
      user: {
        id: 'd-1',
        email: 'driver@nim.co.nz',
        displayName: 'Driver',
        role: 'Driver',
        locale: 'en-NZ',
      },
    });

    const adminRoute = {
      data: { roles: ['Admin', 'Dispatcher'] },
    } as unknown as ActivatedRouteSnapshot;
    const result = TestBed.runInInjectionContext(() => roleGuard(adminRoute, {} as any));

    expect(result instanceof UrlTree).toBe(true);
    expect((result as UrlTree).toString()).toBe('/driver');
  });

  it('redirects Admin cleanly to /admin when accessing driver routes', () => {
    authService.setSession({
      accessToken: 'admin-token',
      expiresIn: 3600,
      tokenType: 'Bearer',
      user: {
        id: 'a-1',
        email: 'admin@nim.co.nz',
        displayName: 'Admin',
        role: 'Admin',
        locale: 'en-NZ',
      },
    });

    const driverRoute = { data: { roles: ['Driver'] } } as unknown as ActivatedRouteSnapshot;
    const result = TestBed.runInInjectionContext(() => roleGuard(driverRoute, {} as any));

    expect(result instanceof UrlTree).toBe(true);
    expect((result as UrlTree).toString()).toBe('/admin');
  });

  it('allows access when role matches expected roles', () => {
    authService.setSession({
      accessToken: 'dispatcher-token',
      expiresIn: 3600,
      tokenType: 'Bearer',
      user: {
        id: 'disp-1',
        email: 'disp@nim.co.nz',
        displayName: 'Dispatcher',
        role: 'Dispatcher',
        locale: 'zh-CN',
      },
    });

    const route = { data: { roles: ['Admin', 'Dispatcher'] } } as unknown as ActivatedRouteSnapshot;
    const result = TestBed.runInInjectionContext(() => roleGuard(route, {} as any));

    expect(result).toBe(true);
  });
});
