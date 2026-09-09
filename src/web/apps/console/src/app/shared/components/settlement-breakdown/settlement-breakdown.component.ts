import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PayslipSettlement } from '../../../core/payroll/settlement.models';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleCurrencyPipe } from '../../../core/i18n/locale-currency.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
@Component({
  selector: 'nim-settlement-breakdown',
  standalone: true,
  imports: [I18nPipe, LocaleCurrencyPipe, LocaleDatePipe],
  templateUrl: './settlement-breakdown.component.html',
  styleUrl: './settlement-breakdown.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettlementBreakdownComponent {
  readonly settlement = input.required<PayslipSettlement>();
  readonly currency = input('NZD');
}
