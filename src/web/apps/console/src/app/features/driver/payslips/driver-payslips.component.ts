import type { PayslipSettlement } from '../../../core/payroll/settlement.models';
import { SettlementBreakdownComponent } from '../../../shared/components/settlement-breakdown/settlement-breakdown.component';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { LocaleCurrencyPipe } from '../../../core/i18n/locale-currency.pipe';
import { LocaleNumberPipe } from '../../../core/i18n/locale-number.pipe';
import { OfflineCacheService } from '../../../core/offline/offline-cache.service';
import { OfflineQueueService } from '../../../core/offline/offline-queue.service';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';

export interface DriverPayslipItem {
  settlement?: PayslipSettlement | null;
  id: string;
  periodStartsOn: string | null;
  periodEndsOn: string | null;
  payPeriod?: string;
  payDate?: string | null;
  grossPay: number;
  netPay: number | null;
  deductions: number | null;
  deductionCalculationStatus: 'NotCalculated' | 'Calculated';
  totalHours: number;
  hourlyRate: number | null;
  currency: string;
}

@Component({
  selector: 'nim-driver-payslips',
  standalone: true,
  imports: [
    SettlementBreakdownComponent,
    CommonModule,
    I18nPipe,
    LocaleDatePipe,
    LocaleCurrencyPipe,
    LocaleNumberPipe,
    IconComponent,
  ],
  templateUrl: './driver-payslips.component.html',
  styleUrl: './driver-payslips.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DriverPayslipsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly offlineCache = inject(OfflineCacheService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  readonly offlineQueue = inject(OfflineQueueService);

  private loadGeneration = 0;
  readonly payslips = signal<DriverPayslipItem[]>([]);
  readonly isLoading = signal<boolean>(true);
  readonly isUsingCache = signal<boolean>(false);

  ngOnInit(): void {
    this.destroyRef.onDestroy(() => {
      this.loadGeneration++;
    });
    void this.loadPayslips();

    // SignalR Realtime Invalidation Subscription
    this.realtime.invalidation$
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        filter(
          (msg) =>
            msg.kind === 'realtime.reconnected' ||
            msg.kind === 'payslip.finalised' ||
            msg.kind.startsWith('payslip.') ||
            msg.kind.startsWith('payroll.'),
        ),
      )
      .subscribe(() => {
        void this.loadPayslips();
      });
  }

  private normalizePayslip(slip: DriverPayslipItem): DriverPayslipItem {
    const hasExplicitStatus =
      slip.deductionCalculationStatus === 'NotCalculated' ||
      slip.deductionCalculationStatus === 'Calculated';
    const calculated =
      slip.deductionCalculationStatus === 'Calculated' &&
      typeof slip.netPay === 'number' &&
      Number.isFinite(slip.netPay) &&
      typeof slip.deductions === 'number' &&
      Number.isFinite(slip.deductions);
    // Earlier cached responses invented net pay and a rate. Do not reuse those amounts.
    const legacyPeriod = /^(\d{4}-\d{2}-\d{2}) ~ (\d{4}-\d{2}-\d{2})$/.exec(slip.payPeriod || '');
    return {
      ...slip,
      settlement: calculated ? slip.settlement : null,
      periodStartsOn: slip.periodStartsOn || legacyPeriod?.[1] || null,
      periodEndsOn: slip.periodEndsOn || legacyPeriod?.[2] || null,
      deductionCalculationStatus: calculated ? 'Calculated' : 'NotCalculated',
      netPay: calculated ? slip.netPay : null,
      deductions: calculated ? slip.deductions : null,
      hourlyRate: hasExplicitStatus ? slip.hourlyRate : null,
    };
  }

  loadPayslips(): void {
    const generation = ++this.loadGeneration;
    let freshResponseReceived = false;
    const online = this.offlineQueue.isOnline();
    this.isLoading.set(true);

    // Cache can arrive after a failed request or during an offline initial load.
    // A newer load or a successful response always wins over this fallback.
    void this.offlineCache
      .getDriverPayslips<DriverPayslipItem>()
      .then((cached) => {
        if (generation !== this.loadGeneration || freshResponseReceived) return;
        if (cached) {
          this.payslips.set(cached.map((slip) => this.normalizePayslip(slip)));
          this.isUsingCache.set(true);
        }
      })
      .catch(() => {
        // A missing/unreadable cache does not prevent a network response.
      })
      .finally(() => {
        if (!online && generation === this.loadGeneration) this.isLoading.set(false);
      });

    if (online) {
      this.http
        .get<DriverPayslipItem[]>('/api/payroll/my-payslips')
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (data) => {
            if (generation !== this.loadGeneration) return;
            freshResponseReceived = true;
            const payslips = (data || []).map((slip) => this.normalizePayslip(slip));
            this.payslips.set(payslips);
            this.isUsingCache.set(false);
            this.isLoading.set(false);
            void this.offlineCache.cacheDriverPayslips(payslips);
          },
          error: () => {
            if (generation !== this.loadGeneration) return;
            this.isLoading.set(false);
            this.isUsingCache.set(true);
          },
        });
    } else {
      this.isUsingCache.set(true);
    }
  }
}
