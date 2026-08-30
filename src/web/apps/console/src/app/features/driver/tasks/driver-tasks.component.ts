import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { OfflineCacheService } from '../../../core/offline/offline-cache.service';
import { OfflineQueueService } from '../../../core/offline/offline-queue.service';

export interface DriverTaskItem {
  id: string;
  tripNo: string;
  status: 'PENDING' | 'ASSIGNED' | 'IN_PROGRESS' | 'COMPLETED' | 'CANCELLED';
  pickupLocation: string;
  deliveryLocation: string;
  scheduledTime: string;
  vehiclePlate: string;
}

@Component({
  selector: 'nim-driver-tasks',
  standalone: true,
  imports: [CommonModule, I18nPipe, LocaleDatePipe],
  templateUrl: './driver-tasks.component.html',
  styleUrl: './driver-tasks.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DriverTasksComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly offlineCache = inject(OfflineCacheService);
  readonly offlineQueue = inject(OfflineQueueService);

  readonly tasks = signal<DriverTaskItem[]>([]);
  readonly isLoading = signal<boolean>(true);
  readonly isUsingCache = signal<boolean>(false);

  ngOnInit(): void {
    void this.loadTasks();
  }

  async loadTasks(): Promise<void> {
    this.isLoading.set(true);

    // Try loading from offline cache first
    const cached = await this.offlineCache.getDriverTasks<DriverTaskItem>();
    if (cached && cached.length > 0) {
      this.tasks.set(cached);
      this.isUsingCache.set(true);
    }

    if (this.offlineQueue.isOnline()) {
      this.http.get<DriverTaskItem[]>('/api/dispatch/my-tasks').subscribe({
        next: async (data) => {
          this.tasks.set(data);
          this.isUsingCache.set(false);
          this.isLoading.set(false);
          await this.offlineCache.cacheDriverTasks(data);
        },
        error: () => {
          // If request fails (e.g. backend offline), keep cached tasks
          this.isLoading.set(false);
          this.isUsingCache.set(true);
        },
      });
    } else {
      this.isLoading.set(false);
    }
  }

  async updateTaskStatus(
    task: DriverTaskItem,
    nextStatus: 'IN_PROGRESS' | 'COMPLETED',
  ): Promise<void> {
    const updated: DriverTaskItem = { ...task, status: nextStatus };
    this.tasks.update((list) => list.map((t) => (t.id === task.id ? updated : t)));
    await this.offlineCache.cacheDriverTasks(this.tasks());

    await this.offlineQueue.enqueue({
      url: `/api/dispatch/tasks/${task.id}/status`,
      method: 'POST',
      body: { status: nextStatus },
      description: `Task ${task.tripNo} -> ${nextStatus}`,
    });
  }
}
