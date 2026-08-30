import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminDashboardComponent } from './admin-dashboard.component';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { RealtimeService } from '../../../core/realtime/realtime.service';

describe('AdminDashboardComponent', () => {
  let component: AdminDashboardComponent;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AdminDashboardComponent],
      providers: [
        AuthService,
        I18nService,
        RealtimeService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(AdminDashboardComponent);
    component = fixture.componentInstance;
  });

  it('initializes default metrics', () => {
    expect(component.metrics().activeDispatches).toBeGreaterThanOrEqual(0);
    expect(component.metrics().onlineDrivers).toBeGreaterThanOrEqual(0);
  });
});
