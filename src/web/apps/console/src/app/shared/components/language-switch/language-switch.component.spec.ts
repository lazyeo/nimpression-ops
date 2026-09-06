import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { LanguageSwitchComponent } from './language-switch.component';
import { I18nService } from '../../../core/i18n/i18n.service';
import { AuthService } from '../../../core/auth/auth.service';

describe('LanguageSwitchComponent', () => {
  let component: LanguageSwitchComponent;
  let fixture: ComponentFixture<LanguageSwitchComponent>;
  let i18nService: I18nService;
  let authService: AuthService;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [LanguageSwitchComponent],
      providers: [
        I18nService,
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    }).compileComponents();

    i18nService = TestBed.inject(I18nService);
    authService = TestBed.inject(AuthService);

    i18nService.setDictionary('en-NZ', {
      LANG: {
        LABEL: 'Language',
        EN_NZ: 'English (NZ)',
        ZH_CN: '\u4e2d\u6587 (\u7b80\u4f53)',
      },
    });
    i18nService.setDictionary('zh-CN', {
      LANG: {
        LABEL: '\u8bed\u8a00\u8bbe\u7f6e',
        EN_NZ: 'English (NZ)',
        ZH_CN: '\u4e2d\u6587 (\u7b80\u4f53)',
      },
    });

    void i18nService.setLanguage('en-NZ');

    fixture = TestBed.createComponent(LanguageSwitchComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders both language options simultaneously with accessible group role', () => {
    const el = fixture.nativeElement as HTMLElement;
    const group = el.querySelector('.lang-switch-group');
    expect(group).toBeTruthy();
    expect(group?.getAttribute('role')).toBe('group');
    expect(group?.getAttribute('aria-label')).toBe('Language');

    const buttons = el.querySelectorAll('button.lang-btn');
    expect(buttons.length).toBe(2);
    expect(buttons[0].textContent?.trim()).toContain('English (NZ)');
    expect(buttons[1].textContent?.trim()).toContain('\u4e2d\u6587 (\u7b80\u4f53)');
  });

  it('sets active state and aria-pressed on currently selected language', () => {
    const el = fixture.nativeElement as HTMLElement;
    const buttons = el.querySelectorAll('button.lang-btn');

    expect(buttons[0].classList.contains('active')).toBe(true);
    expect(buttons[0].getAttribute('aria-pressed')).toBe('true');
    expect(buttons[1].classList.contains('active')).toBe(false);
    expect(buttons[1].getAttribute('aria-pressed')).toBe('false');
  });

  it('explicitly switches language when clicking inactive option', () => {
    const setLanguageSpy = vi.spyOn(i18nService, 'setLanguage');
    const el = fixture.nativeElement as HTMLElement;
    const buttons = el.querySelectorAll('button.lang-btn');

    // Click Chinese button
    (buttons[1] as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(setLanguageSpy).toHaveBeenCalledWith('zh-CN');
    expect(component.currentLang()).toBe('zh-CN');
    expect(buttons[1].classList.contains('active')).toBe(true);
    expect(buttons[1].getAttribute('aria-pressed')).toBe('true');
    expect(buttons[0].classList.contains('active')).toBe(false);
    expect(buttons[0].getAttribute('aria-pressed')).toBe('false');
  });

  it('is a no-op when clicking already active language', () => {
    const setLanguageSpy = vi.spyOn(i18nService, 'setLanguage');
    const el = fixture.nativeElement as HTMLElement;
    const buttons = el.querySelectorAll('button.lang-btn');

    // Click English button while already in en-NZ
    (buttons[0] as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(setLanguageSpy).not.toHaveBeenCalled();
    expect(component.currentLang()).toBe('en-NZ');
  });

  it('syncs user locale via AuthService when authenticated', () => {
    vi.spyOn(authService, 'isAuthenticated').mockReturnValue(true);
    const updateUserLocaleSpy = vi.spyOn(authService, 'updateUserLocale');

    component.selectLanguage('zh-CN');

    expect(updateUserLocaleSpy).toHaveBeenCalledWith('zh-CN');
  });

  it('applies size-sm class when size is sm', () => {
    fixture.componentRef.setInput('size', 'sm');
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const group = el.querySelector('.lang-switch-group');
    expect(group?.classList.contains('size-sm')).toBe(true);
  });

  it('supports keyboard focus and activation via standard button semantics', () => {
    const el = fixture.nativeElement as HTMLElement;
    const buttons = el.querySelectorAll('button.lang-btn');
    const zhButton = buttons[1] as HTMLButtonElement;

    // Focus on button
    zhButton.focus();
    expect(document.activeElement).toBe(zhButton);

    // Keyboard click / dispatch enter/space
    zhButton.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    zhButton.click();
    fixture.detectChanges();

    expect(component.currentLang()).toBe('zh-CN');
  });
});
