import { formatNzDate } from './nz-date';
import { inject, Injectable } from '@angular/core';
import { I18nService } from './i18n.service';
import { SupportedLang } from '../models/i18n.models';

export type DatePreset =
  | 'short'
  | 'medium'
  | 'long'
  | 'full'
  | 'time'
  | 'timeWithSeconds'
  | 'shortDateTime'
  | 'mediumDateTime'
  | 'datetime'
  | 'fullDateTime';

@Injectable({
  providedIn: 'root',
})
export class FormatService {
  private readonly i18n = inject(I18nService);

  private get currentLocale(): SupportedLang {
    return this.i18n.currentLang();
  }

  formatDate(
    value: Date | string | number | null | undefined,
    preset: DatePreset = 'medium',
    customLocale?: SupportedLang,
  ): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }

    const locale = customLocale || this.currentLocale;

    const optionsMap: Record<DatePreset, Intl.DateTimeFormatOptions> = {
      short: { year: 'numeric', month: '2-digit', day: '2-digit' },
      medium: { year: 'numeric', month: '2-digit', day: '2-digit' },
      long: { year: 'numeric', month: '2-digit', day: '2-digit' },
      full: { weekday: 'long', year: 'numeric', month: '2-digit', day: '2-digit' },
      time: { hour: '2-digit', minute: '2-digit', hour12: locale === 'en-NZ' },
      timeWithSeconds: {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: locale === 'en-NZ',
      },
      shortDateTime: {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        hour12: locale === 'en-NZ',
      },
      mediumDateTime: {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        hour12: locale === 'en-NZ',
      },
      datetime: {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        hour12: locale === 'en-NZ',
      },
      fullDateTime: {
        weekday: 'long',
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        hour12: locale === 'en-NZ',
      },
    };

    return formatNzDate(value, optionsMap[preset]);
  }

  formatTime(
    value: Date | string | number | null | undefined,
    includeSeconds = false,
    customLocale?: SupportedLang,
  ): string {
    return this.formatDate(value, includeSeconds ? 'timeWithSeconds' : 'time', customLocale);
  }

  formatDateTime(
    value: Date | string | number | null | undefined,
    preset: 'short' | 'medium' | 'long' | 'full' = 'medium',
    customLocale?: SupportedLang,
  ): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }
    const presetKey: DatePreset = preset === 'short' ? 'shortDateTime' : 'mediumDateTime';
    return this.formatDate(value, presetKey, customLocale);
  }

  formatCurrency(
    amount: number | null | undefined,
    currency = 'NZD',
    customLocale?: SupportedLang,
  ): string {
    if (amount === null || amount === undefined || isNaN(amount)) {
      return '$0.00';
    }

    const locale = customLocale || this.currentLocale;

    return new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(amount);
  }

  formatNumber(
    value: number | null | undefined,
    minDecimals = 0,
    maxDecimals = 2,
    customLocale?: SupportedLang,
  ): string {
    if (value === null || value === undefined || isNaN(value)) {
      return '0';
    }

    const locale = customLocale || this.currentLocale;

    return new Intl.NumberFormat(locale, {
      minimumFractionDigits: minDecimals,
      maximumFractionDigits: maxDecimals,
    }).format(value);
  }
}
