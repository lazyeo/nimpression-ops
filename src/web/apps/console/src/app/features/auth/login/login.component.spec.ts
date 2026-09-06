import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { LoginComponent } from './login.component';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;

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

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('initializes login form with default values and validation', () => {
    expect(component.loginForm.valid).toBe(false);
    component.loginForm.controls.email.setValue('driver@nim.co.nz');
    component.loginForm.controls.password.setValue('password123');
    expect(component.loginForm.valid).toBe(true);
  });

  it('renders language switch component in brand row', () => {
    const el = fixture.nativeElement as HTMLElement;
    const langSwitch = el.querySelector('nim-language-switch');
    expect(langSwitch).toBeTruthy();
  });
});
