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
    const date = new Date('2026-08-24T00:00:00+12:00'); // 24 Aug 2026

    const enFormatted = formatService.formatDate(date, 'short', 'en-NZ');
    const zhFormatted = formatService.formatDate(date, 'short', 'zh-CN');

    expect(enFormatted).toBe('24/08/2026');
    expect(zhFormatted).toBe('24/08/2026');
  });

  it.each(['en-NZ', 'zh-CN'] as const)(
    'uses New Zealand date order for every date preset in %s',
    (locale) => {
      for (const preset of [
        'short',
        'medium',
        'long',
        'full',
        'shortDateTime',
        'mediumDateTime',
        'datetime',
        'fullDateTime',
      ] as const) {
        expect(formatService.formatDate('2026-09-08T21:00:00Z', preset, locale)).toContain(
          '09/09/2026',
        );
      }
    },
  );

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

  it('formats datetime presets with time components (R3)', () => {
    const date = new Date('2026-09-07T17:59:00+12:00'); // 7 Sep 2026 17:59:00

    const enShortDt = formatService.formatDate(date, 'shortDateTime', 'en-NZ');
    const zhShortDt = formatService.formatDate(date, 'shortDateTime', 'zh-CN');

    // en-NZ short datetime includes date and 12-hour or 24-hour time
    expect(enShortDt).toContain('2026');
    expect(enShortDt).toContain('59');

    // zh-CN short datetime includes date and time
    expect(zhShortDt).toContain('2026');
    expect(zhShortDt).toContain('17:59');

    const enMediumDt = formatService.formatDate(date, 'mediumDateTime', 'en-NZ');
    expect(enMediumDt).toContain('2026');
    expect(enMediumDt).toContain('59');

    const dtMethod = formatService.formatDateTime(date, 'medium', 'en-NZ');
    expect(dtMethod).toContain('2026');
    expect(dtMethod).toContain('59');
  });
});
