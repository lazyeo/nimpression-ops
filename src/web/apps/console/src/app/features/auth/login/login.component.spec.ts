import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { LoginComponent } from './login.component';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';

describe('LoginComponent', () => {
  let component: LoginComponent;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        AuthService,
        I18nService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
  });

  it('initializes login form with default values and validation', () => {
    expect(component.loginForm.valid).toBe(false);
    component.loginForm.controls.email.setValue('driver@nim.co.nz');
    component.loginForm.controls.password.setValue('password123');
    expect(component.loginForm.valid).toBe(true);
  });

  it('toggles language between en-NZ and zh-CN', () => {
    const initial = component.currentLang;
    component.toggleLanguage();
    expect(component.currentLang).not.toBe(initial);
  });
});
