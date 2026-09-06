import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { OfflineCacheService } from '../../../core/offline/offline-cache.service';
import { OfflineQueueService } from '../../../core/offline/offline-queue.service';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';

export interface DriverTaskItem {
  id: string;
  tripNo: string;
  status: 'PENDING' | 'ASSIGNED' | 'IN_PROGRESS' | 'COMPLETED' | 'CANCELLED';
  pickupLocation: string;
  deliveryLocation: string;
  scheduledTime: string;
  vehiclePlate: string;
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

@Component({
  selector: 'nim-driver-tasks',
  standalone: true,
  imports: [CommonModule, I18nPipe, LocaleDatePipe, IconComponent],
  templateUrl: './driver-tasks.component.html',
  styleUrl: './driver-tasks.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DriverTasksComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly offlineCache = inject(OfflineCacheService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  readonly offlineQueue = inject(OfflineQueueService);

  readonly activeTab = signal<'active' | 'history'>('active');
  readonly tasks = signal<DriverTaskItem[]>([]);
  readonly historyTasks = signal<DriverTaskItem[]>([]);
  readonly isLoading = signal<boolean>(true);
  readonly isUsingCache = signal<boolean>(false);

  // Server-side pagination for active view
  readonly activePage = signal<number>(1);
  readonly activePageSize = 20;
  readonly activeTotalCount = signal<number>(0);
  readonly activeTotalPages = signal<number>(1);

  // Server-side pagination for history view
  readonly historyPage = signal<number>(1);
  readonly historyPageSize = 5;
  readonly historyTotalCount = signal<number>(0);
  readonly historyTotalPages = signal<number>(1);

  ngOnInit(): void {
    void this.loadTasks();

    // SignalR Realtime Invalidation Subscription
    this.realtime.invalidation$
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        filter((msg) => msg.kind.startsWith('task.') || msg.kind.startsWith('dispatch.')),
      )
      .subscribe(() => {
        void this.loadTasks();
      });
  }

  setTab(tab: 'active' | 'history'): void {
    this.activeTab.set(tab);
    if (tab === 'history') {
      void this.loadHistory(1);
    } else {
      void this.loadTasks(1);
    }
  }

  loadTasks(page = 1): void {
    if (this.activeTab() === 'history') {
      void this.loadHistory(this.historyPage());
      return;
    }

    this.isLoading.set(true);
    this.activePage.set(page);

    // Try loading from offline cache asynchronously as fallback (page 1)
    if (page === 1) {
      void this.offlineCache.getDriverTasks<DriverTaskItem>().then((cached) => {
        if (this.isLoading() && cached && cached.length > 0) {
          this.tasks.set(cached);
          this.activeTotalCount.set(cached.length);
          this.activeTotalPages.set(1);
          this.isUsingCache.set(true);
        }
      });
    }

    if (this.offlineQueue.isOnline()) {
      this.http
        .get<PaginatedResult<DriverTaskItem>>(
          `/api/dispatch/my-tasks?activeOnly=true&page=${page}&pageSize=${this.activePageSize}`,
        )
        .subscribe({
          next: (data) => {
            this.tasks.set(data.items || []);
            this.activeTotalCount.set(data.totalCount || 0);
            this.activeTotalPages.set(data.totalPages || 1);
            this.isUsingCache.set(false);
            this.isLoading.set(false);
            if (page === 1) {
              void this.offlineCache.cacheDriverTasks(data.items || []);
            }
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

  prevActivePage(): void {
    if (this.activePage() > 1) {
      void this.loadTasks(this.activePage() - 1);
    }
  }

  nextActivePage(): void {
    if (this.activePage() < this.activeTotalPages()) {
      void this.loadTasks(this.activePage() + 1);
    }
  }

  async loadHistory(page = 1): Promise<void> {
    this.isLoading.set(true);
    this.historyPage.set(page);

    if (this.offlineQueue.isOnline()) {
      this.http.get<PaginatedResult<DriverTaskItem>>(`/api/dispatch/my-tasks?activeOnly=false&page=${page}&pageSize=${this.historyPageSize}`).subscribe({
        next: (data) => {
          this.historyTasks.set(data.items || []);
          this.historyTotalCount.set(data.totalCount || 0);
          this.historyTotalPages.set(data.totalPages || 1);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
        },
      });
    } else {
      this.isLoading.set(false);
    }
  }

  prevHistoryPage(): void {
    if (this.historyPage() > 1) {
      void this.loadHistory(this.historyPage() - 1);
    }
  }

  nextHistoryPage(): void {
    if (this.historyPage() < this.historyTotalPages()) {
      void this.loadHistory(this.historyPage() + 1);
    }
  }

  async updateTaskStatus(
    task: DriverTaskItem,
    nextStatus: 'IN_PROGRESS' | 'COMPLETED',
  ): Promise<void> {
    if (nextStatus === 'COMPLETED') {
      this.tasks.update((list) => list.filter((t) => t.id !== task.id));
      this.activeTotalCount.update((count) => Math.max(0, count - 1));
      if (this.activeTab() === 'history') {
        void this.loadHistory(this.historyPage());
      }
    } else {
      const updated: DriverTaskItem = { ...task, status: nextStatus };
      this.tasks.update((list) => list.map((t) => (t.id === task.id ? updated : t)));
    }
    await this.offlineCache.cacheDriverTasks(this.tasks());

    await this.offlineQueue.enqueue({
      url: `/api/dispatch/tasks/${task.id}/status`,
      method: 'POST',
      body: { status: nextStatus },
      description: `Task ${task.tripNo} -> ${nextStatus}`,
    });
  }
}
