import { inject, Injectable } from '@angular/core';
import { I18nService } from './i18n.service';
import { SupportedLang } from '../models/i18n.models';

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
    preset: 'short' | 'medium' | 'long' | 'full' = 'medium',
    customLocale?: SupportedLang,
  ): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }

    const date = typeof value === 'string' || typeof value === 'number' ? new Date(value) : value;
    if (isNaN(date.getTime())) {
      return String(value);
    }

    const locale = customLocale || this.currentLocale;

    const optionsMap: Record<'short' | 'medium' | 'long' | 'full', Intl.DateTimeFormatOptions> = {
      short: { year: 'numeric', month: 'numeric', day: 'numeric' },
      medium: { year: 'numeric', month: 'short', day: 'numeric' },
      long: { year: 'numeric', month: 'long', day: 'numeric' },
      full: { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' },
    };

    return new Intl.DateTimeFormat(locale, optionsMap[preset]).format(date);
  }

  formatTime(
    value: Date | string | number | null | undefined,
    includeSeconds = false,
    customLocale?: SupportedLang,
  ): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }

    const date = typeof value === 'string' || typeof value === 'number' ? new Date(value) : value;
    if (isNaN(date.getTime())) {
      return String(value);
    }

    const locale = customLocale || this.currentLocale;
    const options: Intl.DateTimeFormatOptions = {
      hour: '2-digit',
      minute: '2-digit',
      second: includeSeconds ? '2-digit' : undefined,
      hour12: locale === 'en-NZ',
    };

    return new Intl.DateTimeFormat(locale, options).format(date);
  }

  formatDateTime(
    value: Date | string | number | null | undefined,
    customLocale?: SupportedLang,
  ): string {
    if (!value) return '';
    return `${this.formatDate(value, 'medium', customLocale)} ${this.formatTime(value, false, customLocale)}`;
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
