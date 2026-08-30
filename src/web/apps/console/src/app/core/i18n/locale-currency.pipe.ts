import { inject, Pipe, PipeTransform } from '@angular/core';
import { FormatService } from './format.service';
import { SupportedLang } from '../models/i18n.models';

@Pipe({
  name: 'localeCurrency',
  standalone: true,
  pure: false,
})
export class LocaleCurrencyPipe implements PipeTransform {
  private readonly formatService = inject(FormatService);

  transform(
    amount: number | null | undefined,
    currency = 'NZD',
    customLocale?: SupportedLang,
  ): string {
    return this.formatService.formatCurrency(amount, currency, customLocale);
  }
}
