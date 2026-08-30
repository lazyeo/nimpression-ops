import { inject, Pipe, PipeTransform } from '@angular/core';
import { FormatService } from './format.service';
import { SupportedLang } from '../models/i18n.models';

@Pipe({
  name: 'localeNumber',
  standalone: true,
  pure: false,
})
export class LocaleNumberPipe implements PipeTransform {
  private readonly formatService = inject(FormatService);

  transform(
    value: number | null | undefined,
    minDecimals = 0,
    maxDecimals = 2,
    customLocale?: SupportedLang,
  ): string {
    return this.formatService.formatNumber(value, minDecimals, maxDecimals, customLocale);
  }
}
