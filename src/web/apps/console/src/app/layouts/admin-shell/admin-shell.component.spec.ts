import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router, NavigationEnd } from '@angular/router';
import { Subject, of } from 'rxjs';
import { AdminShellComponent } from './admin-shell.component';
import { AuthService } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { RealtimeService } from '../../core/realtime/realtime.service';

describe('AdminShellComponent', () => {
  let component: AdminShellComponent;
  let fixture: ComponentFixture<AdminShellComponent>;
  let router: Router;
  let authService: AuthService;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AdminShellComponent],
      providers: [
        AuthService,
        I18nService,
        RealtimeService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AdminShellComponent);
    component = fixture.componentInstance;
    router = TestBed.inject(Router);
    authService = TestBed.inject(AuthService);
    fixture.detectChanges();
  });

  it('renders admin navigation items', () => {
    expect(component.navItems.length).toBeGreaterThanOrEqual(10);
    expect(component.navItems.some((item) => item.path === '/admin/dispatch')).toBe(true);
    expect(component.navItems.some((item) => item.path === '/admin/drivers')).toBe(true);
  });

  it('initializes drawer in closed state', () => {
    expect(component.sidebarOpen()).toBe(false);

    const aside = fixture.nativeElement.querySelector('#admin-sidebar');
    expect(aside).not.toBeNull();
    expect(aside.classList.contains('drawer-open')).toBe(false);

    const toggleBtn = fixture.nativeElement.querySelector('.sidebar-toggle-btn');
    expect(toggleBtn).not.toBeNull();
    expect(toggleBtn.getAttribute('aria-expanded')).toBe('false');
    expect(toggleBtn.getAttribute('aria-controls')).toBe('admin-sidebar');
  });

  it('toggles, opens, and closes sidebar drawer', () => {
    component.openSidebar();
    expect(component.sidebarOpen()).toBe(true);
    fixture.detectChanges();

    let aside = fixture.nativeElement.querySelector('#admin-sidebar');
    let toggleBtn = fixture.nativeElement.querySelector('.sidebar-toggle-btn');
    expect(aside.classList.contains('drawer-open')).toBe(true);
    expect(toggleBtn.getAttribute('aria-expanded')).toBe('true');

    component.closeSidebar();
    expect(component.sidebarOpen()).toBe(false);
    fixture.detectChanges();

    aside = fixture.nativeElement.querySelector('#admin-sidebar');
    expect(aside.classList.contains('drawer-open')).toBe(false);

    component.toggleSidebar();
    expect(component.sidebarOpen()).toBe(true);

    component.toggleSidebar();
    expect(component.sidebarOpen()).toBe(false);
  });

  it('renders backdrop overlay when drawer is open and closes on backdrop click', () => {
    expect(fixture.nativeElement.querySelector('.sidebar-backdrop')).toBeNull();

    component.openSidebar();
    fixture.detectChanges();

    const backdrop = fixture.nativeElement.querySelector('.sidebar-backdrop');
    expect(backdrop).not.toBeNull();

    backdrop.click();
    expect(component.sidebarOpen()).toBe(false);
  });

  it('closes drawer when drawer close button is clicked', () => {
    component.openSidebar();
    fixture.detectChanges();

    const closeBtn = fixture.nativeElement.querySelector('.drawer-close-btn');
    expect(closeBtn).not.toBeNull();

    closeBtn.click();
    expect(component.sidebarOpen()).toBe(false);
  });

  it('closes drawer on Escape keydown', () => {
    component.openSidebar();
    expect(component.sidebarOpen()).toBe(true);

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    expect(component.sidebarOpen()).toBe(false);
  });

  it('closes drawer when a nav link is clicked', () => {
    component.openSidebar();
    fixture.detectChanges();

    const firstNavLink = fixture.nativeElement.querySelector('.nav-link');
    expect(firstNavLink).not.toBeNull();

    firstNavLink.click();
    expect(component.sidebarOpen()).toBe(false);
  });

  it('closes drawer on router NavigationEnd event', () => {
    component.openSidebar();
    expect(component.sidebarOpen()).toBe(true);

    const routerEvents = router.events as Subject<unknown>;
    routerEvents.next(new NavigationEnd(1, '/admin/drivers', '/admin/drivers'));

    expect(component.sidebarOpen()).toBe(false);
  });

  it('renders header controls on single line without missing actions', () => {
    const offlineStatus = fixture.nativeElement.querySelector('nim-offline-status');
    const langSwitch = fixture.nativeElement.querySelector('nim-language-switch');
    const logoutBtn = fixture.nativeElement.querySelector('.btn-logout');

    expect(offlineStatus).not.toBeNull();
    expect(langSwitch).not.toBeNull();
    expect(logoutBtn).not.toBeNull();
  });

  it('calls authService.logout on logout button click', () => {
    const logoutSpy = vi.spyOn(authService, 'logout').mockReturnValue(of(void 0));
    const logoutBtn = fixture.nativeElement.querySelector('.btn-logout');

    logoutBtn.click();
    expect(logoutSpy).toHaveBeenCalled();
  });

  it('displays payroll nav item for Admin role', () => {
    authService.setSession({
      accessToken: 'dev-only-insecure-admin-token',
      expiresIn: 3600,
      tokenType: 'Bearer',
      user: {
        id: 'a-1',
        email: 'admin@nim.co.nz',
        displayName: 'Admin User',
        role: 'Admin',
        locale: 'en-NZ',
      },
    });
    fixture.detectChanges();

    expect(component.navItems.some((item) => item.path === '/admin/payroll')).toBe(true);
  });

  it('hides payroll nav item for Dispatcher role', () => {
    authService.setSession({
      accessToken: 'dev-only-insecure-disp-token',
      expiresIn: 3600,
      tokenType: 'Bearer',
      user: {
        id: 'd-1',
        email: 'disp@nim.co.nz',
        displayName: 'Dispatcher User',
        role: 'Dispatcher',
        locale: 'en-NZ',
      },
    });
    fixture.detectChanges();

    expect(component.navItems.some((item) => item.path === '/admin/payroll')).toBe(false);
  });

  it('displays notifications nav item for Admin role', () => {
    authService.setSession({
      accessToken: 'dev-only-insecure-admin-token',
      expiresIn: 3600,
      tokenType: 'Bearer',
      user: {
        id: 'a-1',
        email: 'admin@nim.co.nz',
        displayName: 'Admin User',
        role: 'Admin',
        locale: 'en-NZ',
      },
    });
    fixture.detectChanges();

    expect(component.navItems.some((item) => item.path === '/admin/notifications')).toBe(true);
  });

  it('displays notifications nav item for Dispatcher role', () => {
    authService.setSession({
      accessToken: 'dev-only-insecure-disp-token',
      expiresIn: 3600,
      tokenType: 'Bearer',
      user: {
        id: 'd-1',
        email: 'disp@nim.co.nz',
        displayName: 'Dispatcher User',
        role: 'Dispatcher',
        locale: 'en-NZ',
      },
    });
    fixture.detectChanges();

    expect(component.navItems.some((item) => item.path === '/admin/notifications')).toBe(true);
  });
});
