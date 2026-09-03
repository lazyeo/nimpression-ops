import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { LocaleCurrencyPipe } from '../../../core/i18n/locale-currency.pipe';
import { LocaleNumberPipe } from '../../../core/i18n/locale-number.pipe';
import { OfflineCacheService } from '../../../core/offline/offline-cache.service';
import { OfflineQueueService } from '../../../core/offline/offline-queue.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';

export interface DriverPayslipItem {
  id: string;
  payPeriod: string;
  payDate: string;
  grossPay: number;
  netPay: number;
  deductions: number;
  totalHours: number;
  hourlyRate: number;
  currency: string;
}

@Component({
  selector: 'nim-driver-payslips',
  standalone: true,
  imports: [
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
  readonly offlineQueue = inject(OfflineQueueService);

  readonly payslips = signal<DriverPayslipItem[]>([]);
  readonly isLoading = signal<boolean>(true);
  readonly isUsingCache = signal<boolean>(false);

  ngOnInit(): void {
    void this.loadPayslips();
  }

  async loadPayslips(): Promise<void> {
    this.isLoading.set(true);

    const cached = await this.offlineCache.getDriverPayslips<DriverPayslipItem>();
    if (cached && cached.length > 0) {
      this.payslips.set(cached);
      this.isUsingCache.set(true);
    }

    if (this.offlineQueue.isOnline()) {
      this.http.get<DriverPayslipItem[]>('/api/payroll/my-payslips').subscribe({
        next: async (data) => {
          this.payslips.set(data);
          this.isUsingCache.set(false);
          this.isLoading.set(false);
          await this.offlineCache.cacheDriverPayslips(data);
        },
        error: () => {
          this.isLoading.set(false);
          this.isUsingCache.set(true);
        },
      });
    } else {
      this.isLoading.set(false);
    }
  }
}
