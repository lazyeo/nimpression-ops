import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DriverProfileComponent } from './driver-profile.component';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { RealtimeService } from '../../../core/realtime/realtime.service';

describe('DriverProfileComponent (Language switcher & profile)', () => {
  let component: DriverProfileComponent;
  let authService: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [DriverProfileComponent],
      providers: [AuthService, I18nService, provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    authService = TestBed.inject(AuthService);
    authService.setSession({
      accessToken: 'dev-only-insecure-token-123',
      expiresIn: 3600,
      tokenType: 'Bearer',
      user: {
        id: 'd-1',
        email: 'driver@nim.co.nz',
        displayName: 'John Driver',
        role: 'Driver',
        locale: 'en-NZ',
      },
    });

    const fixture = TestBed.createComponent(DriverProfileComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('populates profile with user information', () => {
    component.ngOnInit();
    const req = httpMock.expectOne('/api/drivers/d-1');
    req.flush({
      id: 'd-1',
      displayName: 'John Driver',
      email: 'driver@nim.co.nz',
      phone: '+64 21 123 4567',
      emergencyContact: 'Jane - 021 999 8888',
      employeeNo: 'EMP-001',
      licenceClass: 'Class 4 Heavy',
      licenceExpiry: '2027-12-31',
      locale: 'en-NZ',
    });

    expect(component.profile()?.displayName).toBe('John Driver');
    expect(component.profileForm.controls.locale.value).toBe('en-NZ');
    expect(component.profileForm.controls.phone.value).toBe('+64 21 123 4567');
  });

  it('reloads profile when SignalR invalidation arrives for driver entity', async () => {
    component.ngOnInit();
    const initialReq = httpMock.expectOne('/api/drivers/d-1');
    initialReq.flush({
      id: 'd-1',
      displayName: 'John Driver',
      email: 'driver@nim.co.nz',
      phone: '+64 21 123 4567',
      emergencyContact: 'Jane - 021 999 8888',
      employeeNo: 'EMP-001',
      licenceClass: 'Class 4 Heavy',
      licenceExpiry: '2027-12-31',
      locale: 'en-NZ',
    });

    const realtime = TestBed.inject(RealtimeService);
    (realtime as any).invalidationSubject.next({
      kind: 'driver.updated',
      entityId: 'd-1',
      occurredAt: new Date().toISOString(),
    });

    const reloadReq = httpMock.expectOne('/api/drivers/d-1');
    reloadReq.flush({
      id: 'd-1',
      displayName: 'John Updated',
      email: 'driver@nim.co.nz',
      phone: '+64 21 999 0000',
      emergencyContact: 'Jane - 021 999 8888',
      employeeNo: 'EMP-001',
      licenceClass: 'Class 5',
      licenceExpiry: '2028-12-31',
      locale: 'en-NZ',
    });

    expect(component.profile()?.displayName).toBe('John Updated');
    expect(component.profileForm.controls.phone.value).toBe('+64 21 999 0000');
  });
});
