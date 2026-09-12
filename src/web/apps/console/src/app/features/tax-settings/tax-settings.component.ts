import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin, Observable } from 'rxjs';
import { I18nPipe } from '../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../core/i18n/locale-date.pipe';
import { UserFacingErrorService } from '../../core/errors/user-facing-error.service';
import { TaxProfileService } from './tax-profile.service';
import {
  TaxProfile,
  TaxProfileStatus,
  SubmitTaxProfile,
  ApproveTaxProfile,
} from '../../core/payroll/tax-profile.models';
import { WorkerType, HolidayMode, HolidayEligibility } from '../../core/payroll/settlement.models';
import { TaxDeclarationComponent } from './tax-declaration.component';

export function nzCalendarDate(now: Date): string {
  const parts = new Intl.DateTimeFormat('en-NZ', {
    timeZone: 'Pacific/Auckland',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(now);
  const value = (type: string) => parts.find((p) => p.type === type)?.value;
  return `${value('year')}-${value('month')}-${value('day')}`;
}
@Component({
  selector: 'nim-tax-settings',
  standalone: true,
  imports: [FormsModule, RouterLink, I18nPipe, LocaleDatePipe, TaxDeclarationComponent],
  templateUrl: './tax-settings.component.html',
  styleUrl: './tax-settings.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaxSettingsComponent implements OnInit {
  private readonly api = inject(TaxProfileService);
  private readonly errors = inject(UserFacingErrorService);
  private readonly destroyRef = inject(DestroyRef);
  readonly review = inject(ActivatedRoute).snapshot.data['review'] === true;
  readonly records = signal<TaxProfile[]>([]);
  readonly pending = signal<TaxProfile[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly success = signal(false);
  readonly selected = signal<TaxProfile | null>(null);
  readonly total = signal(0);
  readonly loaded = signal(false);
  page = 1;
  status: TaxProfileStatus | '' = this.review ? 'Pending' : '';
  private loadVersion = 0;
  readonly minDate =
    nzCalendarDate(new Date()) > '2026-04-01' ? nzCalendarDate(new Date()) : '2026-04-01';
  readonly maxDate = '2027-03-31';
  effectiveFrom = '';
  workerType: WorkerType | '' = '';
  taxCode = '';
  employeeRequired: boolean | null = null;
  employeeRate: number | null = null;
  employeeReduction = false;
  withholdingRate: number | null = null;
  withholdingVerified = false;
  exemptionVerified = false;
  gstRate: number | null = null;
  gstVerified = false;
  confirmed = false;
  approvalDate = '';
  employerRequired: boolean | null = null;
  employerRate: number | null = null;
  employerReduction = false;
  esctRate: number | null = null;
  esctVerified = false;
  holidayMode: HolidayMode | '' = '';
  holidayEligibility: HolidayEligibility | '' = '';
  eligibilityConfirmed = false;
  writtenAgreement = false;
  approvalConfirmed = false;
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
  readonly employeeRates = [3, 3.5, 4, 6, 8, 10];
  readonly esctRates = [10.5, 17.5, 30, 33, 39];
  readonly statuses = [
    { value: 'Pending', label: 'TAX_PROFILE.PENDING' },
    { value: 'Approved', label: 'TAX_PROFILE.APPROVED' },
    { value: 'Rejected', label: 'TAX_PROFILE.REJECTED' },
    { value: 'Withdrawn', label: 'TAX_PROFILE.WITHDRAWN' },
  ] as const;
  ngOnInit(): void {
    this.load();
  }
  statusKey(status: string): string {
    return this.statuses.find((s) => s.value === status)?.label ?? 'TAX_PROFILE.UNKNOWN';
  }
  validDate(date: string): boolean {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || date < this.minDate || date > this.maxDate)
      return false;
    const instant = new Date(`${date}T00:00:00Z`);
    return Number.isFinite(instant.getTime()) && instant.toISOString().slice(0, 10) === date;
  }

  load(page = this.page): void {
    const version = ++this.loadVersion;
    this.page = page;
    this.loading.set(true);
    this.loaded.set(false);
    this.error.set('');
    const history = this.review
      ? this.api.all(page, this.status || undefined)
      : this.api.mine(page, this.status || undefined);
    const pending = this.review ? this.api.all(1, 'Pending') : this.api.mine(1, 'Pending');
    forkJoin({ history, pending })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          if (version !== this.loadVersion) return;
          this.records.set(result.history.items);
          this.total.set(result.history.totalCount);
          this.pending.set(result.pending.items);
          this.loaded.set(true);
          this.loading.set(false);
        },
        error: (error) => {
          if (version !== this.loadVersion) return;
          this.error.set(this.errors.format(error));
          this.loading.set(false);
        },
      });
  }
  submission(): SubmitTaxProfile | null {
    if (
      this.review ||
      !this.loaded() ||
      this.pending().length ||
      !this.validDate(this.effectiveFrom) ||
      !this.workerType ||
      !this.confirmed
    )
      return null;
    if (this.workerType === 'Employee') {
      if (
        !this.taxCodes.includes(this.taxCode) ||
        this.employeeRequired === null ||
        (this.employeeRequired &&
          (this.employeeRate === null ||
            !this.employeeRates.includes(this.employeeRate) ||
            (this.employeeRate === 3 && !this.employeeReduction)))
      )
        return null;
      return {
        effectiveFrom: this.effectiveFrom,
        confirmed: true,
        declaration: {
          workerType: 'Employee',
          contractor: null,
          employee: {
            taxCode: this.taxCode,
            kiwiSaverEmployee: {
              contributionsRequired: this.employeeRequired,
              rate: this.employeeRequired ? this.employeeRate! / 100 : 0,
              temporaryReductionApproved:
                this.employeeRequired && this.employeeRate === 3 && this.employeeReduction,
            },
          },
        },
      };
    }
    if (
      this.withholdingRate === null ||
      !Number.isFinite(this.withholdingRate) ||
      this.withholdingRate < 0 ||
      this.withholdingRate > 100 ||
      (!this.withholdingVerified && !(this.exemptionVerified && this.withholdingRate === 0)) ||
      this.gstRate === null ||
      ![0, 15].includes(this.gstRate) ||
      !this.gstVerified
    )
      return null;
    return {
      effectiveFrom: this.effectiveFrom,
      confirmed: true,
      declaration: {
        workerType: 'Contractor',
        employee: null,
        contractor: {
          withholdingRate: this.withholdingRate / 100,
          withholdingVerified: this.withholdingVerified,
          exemptionVerified: this.exemptionVerified,
          gstRate: this.gstRate / 100,
          gstTreatmentVerified: this.gstVerified,
        },
      },
    };
  }
  startReview(profile: TaxProfile): void {
    if (!this.review || profile.status !== 'Pending') return;
    this.selected.set(profile);
    this.approvalDate = this.approvalMinDate;
    this.employerRequired = null;
    this.employerRate = null;
    this.employerReduction = false;
    this.esctRate = null;
    this.esctVerified = false;
    this.holidayMode = '';
    this.holidayEligibility = '';
    this.eligibilityConfirmed = false;
    this.writtenAgreement = false;
    this.approvalConfirmed = false;
    this.error.set('');
  }
  get approvalMinDate(): string {
    const requested = this.selected()?.effectiveFrom ?? '';
    return requested > this.minDate ? requested : this.minDate;
  }
  approval(): ApproveTaxProfile | null {
    const profile = this.selected();
    if (
      !this.review ||
      profile?.status !== 'Pending' ||
      !this.validDate(this.approvalDate) ||
      this.approvalDate < this.approvalMinDate ||
      !this.approvalConfirmed
    )
      return null;
    if (profile.declaration.workerType === 'Contractor')
      return {
        effectiveFrom: this.approvalDate,
        kiwiSaverEmployer: null,
        holidayPay: null,
        confirmed: true,
      };
    if (
      this.employerRequired === null ||
      this.employerRate === null ||
      !Number.isFinite(this.employerRate) ||
      this.employerRate < 0 ||
      this.employerRate > 100 ||
      !this.holidayMode ||
      (this.employerRequired && this.employerRate < (this.employerReduction ? 3 : 3.5)) ||
      (this.employerRate > 0 &&
        (this.esctRate === null || !this.esctRates.includes(this.esctRate) || !this.esctVerified))
    )
      return null;
    if (
      this.holidayMode === 'PayAsYouGoEightPercent' &&
      (!this.holidayEligibility || !this.eligibilityConfirmed || !this.writtenAgreement)
    )
      return null;
    return {
      effectiveFrom: this.approvalDate,
      confirmed: true,
      kiwiSaverEmployer: {
        contributionsRequired: this.employerRequired,
        rate: this.employerRate / 100,
        temporaryReductionApproved: this.employerReduction,
        esctRate: this.employerRate > 0 ? this.esctRate! / 100 : null,
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
  }
  submit(): void {
    const request = this.submission();
    if (request) this.mutate(this.api.submit(request));
  }
  withdraw(profile: TaxProfile): void {
    if (!this.review && profile.status === 'Pending') this.mutate(this.api.withdraw(profile.id));
  }
  approve(): void {
    const request = this.approval();
    const profile = this.selected();
    if (request && profile) this.mutate(this.api.approve(profile.id, request));
  }
  reject(): void {
    const profile = this.selected();
    if (this.review && profile?.status === 'Pending') this.mutate(this.api.reject(profile.id));
  }
  private mutate(operation: Observable<TaxProfile>): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.success.set(false);
    operation.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.busy.set(false);
        this.success.set(true);
        this.confirmed = false;
        this.selected.set(null);
        this.load(1);
      },
      error: (error) => {
        this.busy.set(false);
        this.error.set(this.errors.format(error));
      },
    });
  }
}
