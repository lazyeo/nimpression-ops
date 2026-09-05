import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { DriverProfileComponent } from './driver-profile.component';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';

describe('DriverProfileComponent (Language switcher & profile)', () => {
  let component: DriverProfileComponent;
  let authService: AuthService;

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
  });

  it('populates profile with user information', () => {
    component.ngOnInit();
    expect(component.profile()?.displayName).toBe('John Driver');
    expect(component.profileForm.controls.locale.value).toBe('en-NZ');
  });
});
