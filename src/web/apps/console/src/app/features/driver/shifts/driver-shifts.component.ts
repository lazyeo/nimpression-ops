import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { LocaleNumberPipe } from '../../../core/i18n/locale-number.pipe';
import { OfflineCacheService } from '../../../core/offline/offline-cache.service';
import { OfflineQueueService } from '../../../core/offline/offline-queue.service';

export interface ShiftStatusDto {
  id?: string;
  status: 'NOT_STARTED' | 'ACTIVE' | 'ON_BREAK' | 'COMPLETED';
  clockedInAt?: string;
  totalWorkedMinutes: number;
}

@Component({
  selector: 'nim-driver-shifts',
  standalone: true,
  imports: [CommonModule, I18nPipe, LocaleDatePipe, LocaleNumberPipe],
  templateUrl: './driver-shifts.component.html',
  styleUrl: './driver-shifts.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DriverShiftsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly offlineCache = inject(OfflineCacheService);
  readonly offlineQueue = inject(OfflineQueueService);

  readonly currentShift = signal<ShiftStatusDto>({
    status: 'NOT_STARTED',
    totalWorkedMinutes: 0,
  });
  readonly isLoading = signal<boolean>(false);

  ngOnInit(): void {
    void this.loadShiftStatus();
  }

  async loadShiftStatus(): Promise<void> {
    const cached = await this.offlineCache.getCachedData<ShiftStatusDto>('driver_shift_current');
    if (cached) {
      this.currentShift.set(cached);
    }

    if (this.offlineQueue.isOnline()) {
      this.http.get<ShiftStatusDto>('/api/timesheet/current-shift').subscribe({
        next: async (data) => {
          this.currentShift.set(data);
          await this.offlineCache.cacheData('driver_shift_current', data);
        },
        error: () => {
          // Keep cached
        },
      });
    }
  }

  async clockIn(): Promise<void> {
    const now = new Date().toISOString();
    const updated: ShiftStatusDto = {
      status: 'ACTIVE',
      clockedInAt: now,
      totalWorkedMinutes: 0,
    };
    this.currentShift.set(updated);
    await this.offlineCache.cacheData('driver_shift_current', updated);

    await this.offlineQueue.enqueue({
      url: '/api/timesheet/clock-in',
      method: 'POST',
      body: { timestamp: now },
      description: 'Shift Clock-In',
    });
  }

  async clockOut(): Promise<void> {
    const updated: ShiftStatusDto = {
      ...this.currentShift(),
      status: 'COMPLETED',
    };
    this.currentShift.set(updated);
    await this.offlineCache.cacheData('driver_shift_current', updated);

    await this.offlineQueue.enqueue({
      url: '/api/timesheet/clock-out',
      method: 'POST',
      body: { timestamp: new Date().toISOString() },
      description: 'Shift Clock-Out',
    });
  }

  async toggleBreak(): Promise<void> {
    const isBreak = this.currentShift().status === 'ON_BREAK';
    const nextStatus = isBreak ? 'ACTIVE' : 'ON_BREAK';

    const updated: ShiftStatusDto = {
      ...this.currentShift(),
      status: nextStatus,
    };
    this.currentShift.set(updated);
    await this.offlineCache.cacheData('driver_shift_current', updated);

    await this.offlineQueue.enqueue({
      url: isBreak ? '/api/timesheet/end-break' : '/api/timesheet/start-break',
      method: 'POST',
      body: { timestamp: new Date().toISOString() },
      description: isBreak ? 'End Break' : 'Start Break',
    });
  }
}
