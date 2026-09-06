import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { TimesheetsService } from './services/timesheets.service';
import {
  ShiftEntryDto,
  TimesheetSummaryDto,
  ShiftStatus,
  DriverOption,
  AdminCorrectShiftRequest,
} from './models/timesheets.models';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { LocaleNumberPipe } from '../../../core/i18n/locale-number.pipe';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

@Component({
  selector: 'nim-admin-timesheets',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    I18nPipe,
    LocaleDatePipe,
    LocaleNumberPipe,
    IconComponent,
    StatusBadgeComponent,
  ],
  templateUrl: './timesheets.component.html',
  styleUrl: './timesheets.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TimesheetsComponent implements OnInit {
  private readonly timesheetsService = inject(TimesheetsService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);

  readonly isLoading = signal<boolean>(false);
  readonly isSummaryLoading = signal<boolean>(false);
  readonly hasError = signal<boolean>(false);
  readonly isForbidden = signal<boolean>(false);
  readonly errorMessage = signal<string>('');

  readonly timesheets = signal<ShiftEntryDto[]>([]);
  readonly summary = signal<TimesheetSummaryDto | null>(null);
  readonly drivers = signal<DriverOption[]>([]);
  readonly totalRecords = signal<number>(0);

  // Filters
  readonly selectedDriverId = signal<string>('');
  readonly selectedStatus = signal<string>('');
  readonly fromDate = signal<string>('');
  readonly toDate = signal<string>('');
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(20);

  // Dialog states
  readonly isDetailsModalOpen = signal<boolean>(false);
  readonly selectedShift = signal<ShiftEntryDto | null>(null);

  readonly isCorrectModalOpen = signal<boolean>(false);
  readonly correctingShift = signal<ShiftEntryDto | null>(null);
  readonly isSubmittingCorrection = signal<boolean>(false);
  readonly correctionError = signal<string>('');
  readonly correctionSuccess = signal<boolean>(false);

  // Correction form
  correctionClockIn = '';
  correctionClockOut = '';
  correctionBreakMinutes = 0;
  correctionReason = '';

  readonly totalPages = computed(() => {
    return Math.max(1, Math.ceil(this.totalRecords() / this.pageSize()));
  });

  ngOnInit(): void {
    this.loadDrivers();
    this.loadTimesheets();
    this.loadSummary();

    // SignalR Realtime Invalidation Subscription
    this.realtime.invalidation$
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        filter((msg) => msg.kind.startsWith('shift.') || msg.kind.startsWith('timesheet.')),
      )
      .subscribe(() => {
        this.loadTimesheets();
        this.loadSummary();
      });
  }

  loadDrivers(): void {
    this.timesheetsService.getDrivers().subscribe({
      next: (res) => {
        this.drivers.set(res.items || []);
      },
      error: () => {
        // Driver dropdown failure shouldn't block page
      },
    });
  }

  loadTimesheets(): void {
    this.isLoading.set(true);
    this.hasError.set(false);
    this.isForbidden.set(false);
    this.errorMessage.set('');

    this.timesheetsService
      .getTimesheets({
        driverId: this.selectedDriverId() || undefined,
        status: (this.selectedStatus() as ShiftStatus) || undefined,
        fromDate: this.fromDate() || undefined,
        toDate: this.toDate() || undefined,
        page: this.currentPage(),
        pageSize: this.pageSize(),
      })
      .subscribe({
        next: (res) => {
          this.timesheets.set(res.items || []);
          this.totalRecords.set(res.totalCount || 0);
          this.isLoading.set(false);
        },
        error: (err) => {
          this.isLoading.set(false);
          if (err.status === 403) {
            this.isForbidden.set(true);
          } else {
            this.hasError.set(true);
            this.errorMessage.set(err.message || 'Failed to load timesheet records.');
          }
        },
      });
  }

  loadSummary(): void {
    this.isSummaryLoading.set(true);
    this.timesheetsService
      .getTimesheetSummary(
        this.selectedDriverId() || undefined,
        this.fromDate() || undefined,
        this.toDate() || undefined,
      )
      .subscribe({
        next: (res) => {
          this.summary.set(res);
          this.isSummaryLoading.set(false);
        },
        error: () => {
          this.isSummaryLoading.set(false);
        },
      });
  }

  applyFilters(): void {
    this.currentPage.set(1);
    this.loadTimesheets();
    this.loadSummary();
  }

  resetFilters(): void {
    this.selectedDriverId.set('');
    this.selectedStatus.set('');
    this.fromDate.set('');
    this.toDate.set('');
    this.currentPage.set(1);
    this.loadTimesheets();
    this.loadSummary();
  }

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages() && page !== this.currentPage()) {
      this.currentPage.set(page);
      this.loadTimesheets();
    }
  }

  viewDetails(shift: ShiftEntryDto): void {
    this.selectedShift.set(shift);
    this.isDetailsModalOpen.set(true);
  }

  closeDetailsModal(): void {
    this.isDetailsModalOpen.set(false);
    this.selectedShift.set(null);
  }

  openCorrectModal(shift: ShiftEntryDto): void {
    this.correctingShift.set(shift);
    this.correctionClockIn = shift.clockInAt ? this.formatForDateTimeLocal(shift.clockInAt) : '';
    this.correctionClockOut = shift.clockOutAt ? this.formatForDateTimeLocal(shift.clockOutAt) : '';
    this.correctionBreakMinutes = shift.breakMinutes || 0;
    this.correctionReason = '';
    this.correctionError.set('');
    this.correctionSuccess.set(false);
    this.isCorrectModalOpen.set(true);
  }

  closeCorrectModal(): void {
    this.isCorrectModalOpen.set(false);
    this.correctingShift.set(null);
  }

  submitCorrection(): void {
    const shift = this.correctingShift();
    if (!shift) return;

    const trimmedReason = this.correctionReason.trim();
    if (!trimmedReason) {
      this.correctionError.set('Correction reason is mandatory.');
      return;
    }

    this.isSubmittingCorrection.set(true);
    this.correctionError.set('');

    const request: AdminCorrectShiftRequest = {
      newClockInAt: new Date(this.correctionClockIn).toISOString(),
      newClockOutAt: this.correctionClockOut
        ? new Date(this.correctionClockOut).toISOString()
        : null,
      newBreakMinutes: Number(this.correctionBreakMinutes) || 0,
      reason: trimmedReason,
    };

    this.timesheetsService.adminCorrectShift(shift.id, request).subscribe({
      next: (updatedShift) => {
        this.isSubmittingCorrection.set(false);
        this.correctionSuccess.set(true);
        // Update item in local list
        this.timesheets.update((list) =>
          list.map((item) => (item.id === updatedShift.id ? updatedShift : item)),
        );
        this.loadSummary();
        setTimeout(() => {
          this.closeCorrectModal();
        }, 1200);
      },
      error: (err) => {
        this.isSubmittingCorrection.set(false);
        this.correctionError.set(
          err.error?.message || err.message || 'Failed to apply admin correction.',
        );
      },
    });
  }

  private formatForDateTimeLocal(isoString: string): string {
    const d = new Date(isoString);
    if (isNaN(d.getTime())) return '';
    const pad = (n: number) => n.toString().padStart(2, '0');
    const y = d.getFullYear();
    const m = pad(d.getMonth() + 1);
    const day = pad(d.getDate());
    const h = pad(d.getHours());
    const min = pad(d.getMinutes());
    return `${y}-${m}-${day}T${h}:${min}`;
  }
}
