import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AdminShellComponent } from './admin-shell.component';
import { AuthService } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { RealtimeService } from '../../core/realtime/realtime.service';

describe('AdminShellComponent', () => {
  let component: AdminShellComponent;

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
        provideRouter([]),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(AdminShellComponent);
    component = fixture.componentInstance;
  });

  it('renders admin navigation items', () => {
    expect(component.navItems.length).toBeGreaterThanOrEqual(10);
    expect(component.navItems.some((item) => item.path === '/admin/dispatch')).toBe(true);
    expect(component.navItems.some((item) => item.path === '/admin/drivers')).toBe(true);
  });
});
