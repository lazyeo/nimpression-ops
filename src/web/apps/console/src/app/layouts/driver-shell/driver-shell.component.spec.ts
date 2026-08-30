import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { DriverShellComponent } from './driver-shell.component';
import { AuthService } from '../../core/auth/auth.service';
import { I18nService } from '../../core/i18n/i18n.service';
import { RealtimeService } from '../../core/realtime/realtime.service';

describe('DriverShellComponent (Mobile First & Bottom Nav)', () => {
  let component: DriverShellComponent;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [DriverShellComponent],
      providers: [
        AuthService,
        I18nService,
        RealtimeService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(DriverShellComponent);
    component = fixture.componentInstance;
  });

  it('renders bottom navigation with 4 main touch items', () => {
    expect(component.bottomNavItems.length).toBe(4);
    expect(component.bottomNavItems.map((i) => i.path)).toEqual([
      '/driver/tasks',
      '/driver/shifts',
      '/driver/payslips',
      '/driver/profile',
    ]);
  });
});
