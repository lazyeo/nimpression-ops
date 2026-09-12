import { Component, input, ChangeDetectionStrategy } from '@angular/core';
import { TaxProfile } from '../../core/payroll/tax-profile.models';
import { I18nPipe } from '../../core/i18n/i18n.pipe';
import { LocaleNumberPipe } from '../../core/i18n/locale-number.pipe';
@Component({
  selector: 'nim-tax-declaration',
  standalone: true,
  imports: [I18nPipe, LocaleNumberPipe],
  templateUrl: './tax-declaration.component.html',
  styleUrl: './tax-declaration.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaxDeclarationComponent {
  readonly profile = input.required<TaxProfile>();
}
