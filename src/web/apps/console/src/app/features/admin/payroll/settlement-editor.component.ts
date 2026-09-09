import {
  ChangeDetectionStrategy,
  Component,
  OnChanges,
  input,
  output,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { UserFacingErrorService } from '../../../core/errors/user-facing-error.service';
import {
  PayFrequency,
  WorkerType,
  HolidayMode,
  HolidayEligibility,
  SettlementRequest,
} from '../../../core/payroll/settlement.models';
import { PayslipDto } from './models/payroll.models';
import { PayrollService } from './services/payroll.service';

@Component({
  selector: 'nim-settlement-editor',
  standalone: true,
  imports: [FormsModule, I18nPipe],
  templateUrl: './settlement-editor.component.html',
  styleUrl: './settlement-editor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettlementEditorComponent implements OnChanges {
  readonly payslip = input.required<PayslipDto>();
  readonly saved = output<PayslipDto>();
  private readonly payroll = inject(PayrollService);
  private readonly errors = inject(UserFacingErrorService);
  readonly saving = signal(false);
  readonly error = signal('');
  payDate = '';
  frequency: PayFrequency | '' = '';
  workerType: WorkerType | '' = '';
  taxCode = '';
  employeeRequired: boolean | null = null;
  employeeRate: number | null = null;
  employeeReduction = false;
  employerRequired: boolean | null = null;
  employerRate: number | null = null;
  employerReduction = false;
  esctRate: number | null = null;
  esctVerified = false;
  holidayMode: HolidayMode | '' = '';
  holidayEligibility: HolidayEligibility | '' = '';
  eligibilityConfirmed = false;
  writtenAgreement = false;
  withholdingRate: number | null = null;
  withholdingVerified = false;
  exemptionVerified = false;
  gstRate: number | null = null;
  gstVerified = false;
  readonly taxCodes = [
    'M',
    'M SL',
    'ME',
    'ME SL',
    'SB',
    'SB SL',
    'S',
    'S SL',
    'SH',
    'SH SL',
    'ST',
    'ST SL',
    'SA',
    'SA SL',
  ];
  readonly frequencies = [
    { value: 'Weekly', label: 'SETTLEMENT.FREQUENCY_Weekly' },
    { value: 'Fortnightly', label: 'SETTLEMENT.FREQUENCY_Fortnightly' },
    { value: 'FourWeekly', label: 'SETTLEMENT.FREQUENCY_FourWeekly' },
    { value: 'Monthly', label: 'SETTLEMENT.FREQUENCY_Monthly' },
  ] as const;
  readonly employeeRates = [0, 3, 3.5, 4, 6, 8, 10];
  readonly esctRates = [10.5, 17.5, 30, 33, 39];

  ngOnChanges(): void {
    const request = this.payslip().settlement?.request;
    if (!request) return;
    this.payDate = request.payDate;
    this.frequency = request.frequency;
    this.workerType = request.workerType;
    const percent = (rate: number): number => Number((rate * 100).toFixed(8));
    const employee = request.employee;
    this.taxCode = employee?.taxCode ?? '';
    this.employeeRequired = employee?.kiwiSaverEmployee.contributionsRequired ?? null;
    this.employeeRate = employee ? percent(employee.kiwiSaverEmployee.rate) : null;
    this.employeeReduction = employee?.kiwiSaverEmployee.temporaryReductionApproved ?? false;
    this.employerRequired = employee?.kiwiSaverEmployer.contributionsRequired ?? null;
    this.employerRate = employee ? percent(employee.kiwiSaverEmployer.rate) : null;
    this.employerReduction = employee?.kiwiSaverEmployer.temporaryReductionApproved ?? false;
    this.esctRate =
      employee?.kiwiSaverEmployer.esctRate != null
        ? percent(employee.kiwiSaverEmployer.esctRate)
        : null;
    this.esctVerified = employee?.kiwiSaverEmployer.esctRateVerified ?? false;
    this.holidayMode = employee?.holidayPay.mode ?? '';
    this.holidayEligibility = employee?.holidayPay.eligibility ?? '';
    this.eligibilityConfirmed = employee?.holidayPay.eligibilityConfirmed ?? false;
    this.writtenAgreement = employee?.holidayPay.writtenAgreementConfirmed ?? false;
    const contractor = request.contractor;
    this.withholdingRate = contractor ? percent(contractor.withholdingRate) : null;
    this.withholdingVerified = contractor?.withholdingVerified ?? false;
    this.exemptionVerified = contractor?.exemptionVerified ?? false;
    this.gstRate = contractor ? percent(contractor.gstRate) : null;
    this.gstVerified = contractor?.gstTreatmentVerified ?? false;
  }

  buildRequest(): SettlementRequest | null {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(this.payDate) || !this.frequency || !this.workerType)
      return null;
    const request: SettlementRequest = {
      payDate: this.payDate,
      frequency: this.frequency,
      workerType: this.workerType,
      grossEarnings: 0,
      employee: null,
      contractor: null,
    };
    if (this.workerType === 'Employee') {
      if (
        !this.taxCodes.includes(this.taxCode) ||
        this.employeeRequired === null ||
        this.employerRequired === null ||
        this.employeeRate === null ||
        this.employerRate === null ||
        !this.holidayMode
      )
        return null;
      if (
        !this.employeeRates.includes(this.employeeRate) ||
        this.employerRate < 0 ||
        this.employerRate > 100
      )
        return null;
      if (
        this.employeeRequired &&
        (this.employeeRate === 0 || (this.employeeRate === 3 && !this.employeeReduction))
      )
        return null;
      if (!this.employeeRequired && this.employeeRate !== 0) return null;
      if (this.employerRequired && this.employerRate < (this.employerReduction ? 3 : 3.5))
        return null;
      if (
        this.employerRate > 0 &&
        (!this.esctVerified || this.esctRate === null || !this.esctRates.includes(this.esctRate))
      )
        return null;
      if (
        this.holidayMode === 'PayAsYouGoEightPercent' &&
        (!this.holidayEligibility || !this.eligibilityConfirmed || !this.writtenAgreement)
      )
        return null;
      request.employee = {
        taxCode: this.taxCode,
        kiwiSaverEmployee: {
          rate: this.employeeRate / 100,
          contributionsRequired: this.employeeRequired,
          temporaryReductionApproved: this.employeeReduction,
        },
        kiwiSaverEmployer: {
          rate: this.employerRate / 100,
          contributionsRequired: this.employerRequired,
          temporaryReductionApproved: this.employerReduction,
          esctRate: this.employerRate > 0 && this.esctRate !== null ? this.esctRate / 100 : null,
          esctRateVerified: this.employerRate > 0 && this.esctVerified,
        },
        holidayPay: {
          mode: this.holidayMode,
          eligibility:
            this.holidayMode === 'PayAsYouGoEightPercent' ? this.holidayEligibility || null : null,
          eligibilityConfirmed:
            this.holidayMode === 'PayAsYouGoEightPercent' && this.eligibilityConfirmed,
          writtenAgreementConfirmed:
            this.holidayMode === 'PayAsYouGoEightPercent' && this.writtenAgreement,
        },
      };
    } else {
      if (
        this.withholdingRate === null ||
        this.withholdingRate < 0 ||
        this.withholdingRate > 100 ||
        (!this.withholdingVerified && !(this.exemptionVerified && this.withholdingRate === 0)) ||
        this.gstRate === null ||
        ![0, 15].includes(this.gstRate) ||
        !this.gstVerified
      )
        return null;
      request.contractor = {
        withholdingRate: this.withholdingRate / 100,
        withholdingVerified: this.withholdingVerified,
        exemptionVerified: this.exemptionVerified,
        gstRate: this.gstRate / 100,
        gstTreatmentVerified: this.gstVerified,
      };
    }
    return request;
  }
  submit(): void {
    const request = this.buildRequest();
    if (
      !request ||
      this.saving() ||
      this.payslip().finalisedAt ||
      this.payslip().currency !== 'NZD'
    )
      return;
    this.saving.set(true);
    this.error.set('');
    this.payroll.calculateSettlement(this.payslip().id, request).subscribe({
      next: (slip) => {
        this.saving.set(false);
        this.saved.emit(slip);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(this.errors.format(error));
      },
    });
  }
}
