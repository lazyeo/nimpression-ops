import { inject, Pipe, PipeTransform } from '@angular/core';
import { FormatService } from './format.service';
import { SupportedLang } from '../models/i18n.models';

@Pipe({
  name: 'localeDate',
  standalone: true,
  pure: false,
})
export class LocaleDatePipe implements PipeTransform {
  private readonly formatService = inject(FormatService);

  transform(
    value: Date | string | number | null | undefined,
    preset: 'short' | 'medium' | 'long' | 'full' = 'medium',
    customLocale?: SupportedLang,
  ): string {
    return this.formatService.formatDate(value, preset, customLocale);
  }
}
