import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { FormatService } from './format.service';
import { I18nService } from './i18n.service';

describe('FormatService (F13.4)', () => {
  let formatService: FormatService;
  let i18nService: I18nService;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [FormatService, I18nService, provideHttpClient(), provideHttpClientTesting()],
    });
    formatService = TestBed.inject(FormatService);
    i18nService = TestBed.inject(I18nService);
  });

  it('formats dates according to en-NZ and zh-CN', () => {
    const date = new Date(2026, 7, 24); // 24 Aug 2026

    const enFormatted = formatService.formatDate(date, 'short', 'en-NZ');
    const zhFormatted = formatService.formatDate(date, 'short', 'zh-CN');

    expect(enFormatted).toContain('24');
    expect(enFormatted).toContain('8');
    expect(enFormatted).toContain('2026');

    expect(zhFormatted).toContain('2026');
    expect(zhFormatted).toContain('8');
    expect(zhFormatted).toContain('24');
  });

  it('formats currencies according to en-NZ and zh-CN', () => {
    const amount = 1234.5;
    const enCurrency = formatService.formatCurrency(amount, 'NZD', 'en-NZ');
    const zhCurrency = formatService.formatCurrency(amount, 'NZD', 'zh-CN');

    expect(enCurrency).toContain('1,234.50');
    expect(enCurrency).toContain('$');

    expect(zhCurrency).toContain('1,234.50');
  });

  it('formats numbers with standard decimal places', () => {
    const num = 1234567.891;
    const formatted = formatService.formatNumber(num, 2, 2, 'en-NZ');
    expect(formatted).toBe('1,234,567.89');
  });

  it('reactively uses the active i18n language', () => {
    const amount = 500;
    i18nService.setLanguage('en-NZ');
    const en = formatService.formatCurrency(amount, 'NZD');
    expect(en).toContain('$500.00');

    i18nService.setLanguage('zh-CN');
    const zh = formatService.formatCurrency(amount, 'NZD');
    expect(zh).toContain('500.00');
  });
});
