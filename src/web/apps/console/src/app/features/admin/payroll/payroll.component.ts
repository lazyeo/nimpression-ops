import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PayrollService } from './services/payroll.service';
import {
  PayPeriodDto,
  PayslipDto,
  PayPeriodStatus,
  CreatePayPeriodRequest,
  CalculatePayrollRequest,
  VoidPayPeriodRequest,
} from './models/payroll.models';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { LocaleCurrencyPipe } from '../../../core/i18n/locale-currency.pipe';
import { LocaleNumberPipe } from '../../../core/i18n/locale-number.pipe';
import { IconComponent } from '../../../shared/components/icon/icon.component';

@Component({
  selector: 'nim-admin-payroll',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    I18nPipe,
    LocaleDatePipe,
    LocaleCurrencyPipe,
    LocaleNumberPipe,
    IconComponent,
  ],
  templateUrl: './payroll.component.html',
  styleUrl: './payroll.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PayrollComponent implements OnInit {
  private readonly payrollService = inject(PayrollService);

  readonly isLoading = signal<boolean>(false);
  readonly isPayslipsLoading = signal<boolean>(false);
  readonly isDetailLoading = signal<boolean>(false);
  readonly hasError = signal<boolean>(false);
  readonly isForbidden = signal<boolean>(false);
  readonly errorMessage = signal<string>('');

  // Main data
  readonly payPeriods = signal<PayPeriodDto[]>([]);
  readonly totalRecords = signal<number>(0);
  readonly selectedPeriod = signal<PayPeriodDto | null>(null);
  readonly periodPayslips = signal<PayslipDto[]>([]);
  readonly activePayslip = signal<PayslipDto | null>(null);

  // View state: 'periods' | 'payslips'
  readonly currentView = signal<'periods' | 'payslips'>('periods');
  readonly activeTraceTab = signal<'shifts' | 'trips' | 'lines'>('shifts');

  // Filters
  readonly selectedStatus = signal<string>('');
  readonly fromDate = signal<string>('');
  readonly toDate = signal<string>('');
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(20);

  // Modal dialog states
  readonly isCreateModalOpen = signal<boolean>(false);
  readonly isCalculateModalOpen = signal<boolean>(false);
  readonly isFinaliseModalOpen = signal<boolean>(false);
  readonly isVoidModalOpen = signal<boolean>(false);
  readonly isDetailModalOpen = signal<boolean>(false);
  readonly isActionInProgress = signal<boolean>(false);
  readonly actionError = signal<string>('');

  // Form fields
  newStartsOn = '';
  newEndsOn = '';
  calcMinHourlyWage: number | null = null;
  voidReason = '';
  targetPeriodId: string | null = null;

  readonly totalPages = computed(() => {
    return Math.max(1, Math.ceil(this.totalRecords() / this.pageSize()));
  });

  ngOnInit(): void {
    this.loadPayPeriods();
  }

  loadPayPeriods(): void {
    this.isLoading.set(true);
    this.hasError.set(false);
    this.isForbidden.set(false);
    this.errorMessage.set('');

    this.payrollService
      .getPayPeriods({
        status: (this.selectedStatus() as PayPeriodStatus) || undefined,
        fromDate: this.fromDate() || undefined,
        toDate: this.toDate() || undefined,
        page: this.currentPage(),
        pageSize: this.pageSize(),
      })
      .subscribe({
        next: (res) => {
          this.payPeriods.set(res.items || []);
          this.totalRecords.set(res.totalCount || 0);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.isLoading.set(false);
          if (err.status === 403) {
            this.isForbidden.set(true);
          } else {
            this.hasError.set(true);
            this.errorMessage.set(err.message || 'Failed to load pay periods.');
          }
        },
      });
  }

  applyFilters(): void {
    this.currentPage.set(1);
    this.loadPayPeriods();
  }

  resetFilters(): void {
    this.selectedStatus.set('');
    this.fromDate.set('');
    this.toDate.set('');
    this.currentPage.set(1);
    this.loadPayPeriods();
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages() && page !== this.currentPage()) {
      this.currentPage.set(page);
      this.loadPayPeriods();
    }
  }

  // Drilldown to Payslips for a period
  viewPeriodPayslips(period: PayPeriodDto): void {
    this.selectedPeriod.set(period);
    this.currentView.set('payslips');
    this.isPayslipsLoading.set(true);

    this.payrollService.getPayPeriodPayslips(period.id).subscribe({
      next: (payslips) => {
        this.periodPayslips.set(payslips || []);
        this.isPayslipsLoading.set(false);
      },
      error: (err) => {
        this.isPayslipsLoading.set(false);
        if (err.status === 403) {
          this.isForbidden.set(true);
        } else {
          this.hasError.set(true);
          this.errorMessage.set(err.message || 'Failed to load payslips.');
        }
      },
    });
  }

  backToPeriods(): void {
    this.currentView.set('periods');
    this.selectedPeriod.set(null);
    this.periodPayslips.set([]);
  }

  // Payslip detail view
  viewPayslipDetail(payslip: PayslipDto): void {
    this.isDetailLoading.set(true);
    this.isDetailModalOpen.set(true);
    this.activeTraceTab.set('shifts');

    this.payrollService.getPayslipById(payslip.id).subscribe({
      next: (fullDetail) => {
        this.activePayslip.set(fullDetail);
        this.isDetailLoading.set(false);
      },
      error: () => {
        // Fallback to the summary object if endpoint fails
        this.activePayslip.set(payslip);
        this.isDetailLoading.set(false);
      },
    });
  }

  closeDetailModal(): void {
    this.isDetailModalOpen.set(false);
    this.activePayslip.set(null);
  }

  // Dialog actions
  openCreateModal(): void {
    this.newStartsOn = '';
    this.newEndsOn = '';
    this.actionError.set('');
    this.isCreateModalOpen.set(true);
  }

  closeCreateModal(): void {
    this.isCreateModalOpen.set(false);
  }

  submitCreatePeriod(): void {
    if (!this.newStartsOn) return;

    this.isActionInProgress.set(true);
    this.actionError.set('');

    const request: CreatePayPeriodRequest = {
      startsOn: this.newStartsOn,
      endsOn: this.newEndsOn || null,
    };

    this.payrollService.createPayPeriod(request).subscribe({
      next: () => {
        this.isActionInProgress.set(false);
        this.closeCreateModal();
        this.loadPayPeriods();
      },
      error: (err) => {
        this.isActionInProgress.set(false);
        this.actionError.set(err.error?.message || err.message || 'Failed to create pay period.');
      },
    });
  }

  openCalculateModal(period: PayPeriodDto): void {
    this.targetPeriodId = period.id;
    this.calcMinHourlyWage = null;
    this.actionError.set('');
    this.isCalculateModalOpen.set(true);
  }

  closeCalculateModal(): void {
    this.isCalculateModalOpen.set(false);
    this.targetPeriodId = null;
  }

  submitCalculatePayroll(): void {
    if (!this.targetPeriodId) return;

    this.isActionInProgress.set(true);
    this.actionError.set('');

    const req: CalculatePayrollRequest = {
      minimumHourlyWage: this.calcMinHourlyWage ? Number(this.calcMinHourlyWage) : null,
    };

    this.payrollService.calculatePayroll(this.targetPeriodId, req).subscribe({
      next: () => {
        this.isActionInProgress.set(false);
        this.closeCalculateModal();
        this.loadPayPeriods();
        if (this.selectedPeriod() && this.selectedPeriod()?.id === this.targetPeriodId) {
          this.viewPeriodPayslips(this.selectedPeriod()!);
        }
      },
      error: (err) => {
        this.isActionInProgress.set(false);
        this.actionError.set(err.error?.message || err.message || 'Failed to calculate payroll.');
      },
    });
  }

  openFinaliseModal(period: PayPeriodDto): void {
    this.targetPeriodId = period.id;
    this.actionError.set('');
    this.isFinaliseModalOpen.set(true);
  }

  closeFinaliseModal(): void {
    this.isFinaliseModalOpen.set(false);
    this.targetPeriodId = null;
  }

  submitFinalisePeriod(): void {
    if (!this.targetPeriodId) return;

    this.isActionInProgress.set(true);
    this.actionError.set('');

    this.payrollService.finalisePayPeriod(this.targetPeriodId).subscribe({
      next: () => {
        this.isActionInProgress.set(false);
        this.closeFinaliseModal();
        this.loadPayPeriods();
      },
      error: (err) => {
        this.isActionInProgress.set(false);
        this.actionError.set(err.error?.message || err.message || 'Failed to finalise pay period.');
      },
    });
  }

  openVoidModal(period: PayPeriodDto): void {
    this.targetPeriodId = period.id;
    this.voidReason = '';
    this.actionError.set('');
    this.isVoidModalOpen.set(true);
  }

  closeVoidModal(): void {
    this.isVoidModalOpen.set(false);
    this.targetPeriodId = null;
  }

  submitVoidPeriod(): void {
    if (!this.targetPeriodId || !this.voidReason.trim()) return;

    this.isActionInProgress.set(true);
    this.actionError.set('');

    const req: VoidPayPeriodRequest = {
      reason: this.voidReason.trim(),
    };

    this.payrollService.voidPayPeriod(this.targetPeriodId, req).subscribe({
      next: () => {
        this.isActionInProgress.set(false);
        this.closeVoidModal();
        this.loadPayPeriods();
      },
      error: (err) => {
        this.isActionInProgress.set(false);
        this.actionError.set(err.error?.message || err.message || 'Failed to void pay period.');
      },
    });
  }
}
