import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
  computed,
  ElementRef,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter, Subscription } from 'rxjs';
import { UserFacingErrorService } from '../../../core/errors/user-facing-error.service';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { OfflineCacheService } from '../../../core/offline/offline-cache.service';
import { OfflineQueueService } from '../../../core/offline/offline-queue.service';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { toScreamingSnake } from '../../../core/utils/case.utils';

export interface DriverTaskItem {
  id: string;
  tripNo: string;
  status: 'PENDING' | 'ASSIGNED' | 'ACKNOWLEDGED' | 'IN_PROGRESS' | 'COMPLETED' | 'CANCELLED';
  pickupLocation: string;
  deliveryLocation: string;
  scheduledTime: string;
  vehiclePlate: string;
}

export interface DriverTaskDetail {
  id: string;
  ref: string;
  title: string;
  description: string | null;
  areaName: string;
  areaCode: string;
  vehicleRego: string | null;
  scheduledFor: string;
  status: string;
  priority: string;
  acknowledgedAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  plannedDistanceKm: number | null;
  actualDistanceKm: number | null;
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
  private readonly userErrors = inject(UserFacingErrorService);
  @ViewChild('taskDialog', { static: true }) private taskDialog!: ElementRef<HTMLDialogElement>;
  private detailRequest?: Subscription;
  readonly selectedTaskId = signal<string | null>(null);
  readonly taskDetail = signal<DriverTaskDetail | null>(null);
  readonly detailLoading = signal(false);
  readonly detailError = signal<string | null>(null);
  private readonly offlineCache = inject(OfflineCacheService);
  private readonly realtime = inject(RealtimeService);
  private readonly destroyRef = inject(DestroyRef);
  readonly toScreamingSnake = toScreamingSnake;
  readonly offlineQueue = inject(OfflineQueueService);

  readonly activeTab = signal<'active' | 'history'>('active');
  readonly tasks = signal<DriverTaskItem[]>([]);
  readonly historyTasks = signal<DriverTaskItem[]>([]);
  private readonly activeLoading = signal(true);
  private readonly historyLoading = signal(false);
  readonly isLoading = computed(() =>
    this.activeTab() === 'active' ? this.activeLoading() : this.historyLoading(),
  );
  private activeLoadGeneration = 0;
  private historyLoadGeneration = 0;
  private activeRequest?: Subscription;
  private historyRequest?: Subscription;
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
        filter(
          (msg) =>
            msg.kind === 'realtime.reconnected' ||
            msg.kind.startsWith('task.') ||
            msg.kind.startsWith('dispatch.'),
        ),
      )
      .subscribe((msg) => {
        if (msg.kind === 'realtime.reconnected') {
          this.loadActiveTasks(this.activePage());
          void this.loadHistory(this.historyPage());
        } else {
          void this.loadTasks();
        }
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
    this.loadActiveTasks(page);
  }

  private loadActiveTasks(page: number): void {
    const generation = ++this.activeLoadGeneration;
    this.activeRequest?.unsubscribe();
    this.activeLoading.set(true);
    this.activePage.set(page);
    let serverSucceeded = false;
    const isCurrent = () => !this.destroyRef.destroyed && generation === this.activeLoadGeneration;

    // Try loading from offline cache asynchronously as fallback (page 1)
    if (page === 1) {
      void this.offlineCache
        .getDriverTasks<DriverTaskItem>()
        .then((cached) => {
          if (isCurrent() && !serverSucceeded && cached !== null) {
            this.tasks.set(cached);
            this.activeTotalCount.set(cached.length);
            this.activeTotalPages.set(1);
            this.isUsingCache.set(true);
          }
        })
        .catch(() => {
          // Cache is optional; a successful current HTTP response remains authoritative.
        });
    }

    if (this.offlineQueue.isOnline()) {
      this.activeRequest = this.http
        .get<PaginatedResult<DriverTaskItem>>(
          `/api/dispatch/my-tasks?activeOnly=true&page=${page}&pageSize=${this.activePageSize}`,
        )
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (data) => {
            if (!isCurrent()) return;
            serverSucceeded = true;
            this.tasks.set(data.items || []);
            this.activeTotalCount.set(data.totalCount || 0);
            this.activeTotalPages.set(data.totalPages || 1);
            this.isUsingCache.set(false);
            this.activeLoading.set(false);
            if (page === 1) {
              void this.offlineCache.cacheDriverTasks(data.items || []);
            }
          },
          error: () => {
            if (!isCurrent()) return;
            // If request fails (e.g. backend offline), keep cached tasks
            this.activeLoading.set(false);
            this.isUsingCache.set(true);
          },
        });
    } else {
      this.activeLoading.set(false);
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
    const generation = ++this.historyLoadGeneration;
    this.historyRequest?.unsubscribe();
    this.historyLoading.set(true);
    this.historyPage.set(page);
    const isCurrent = () => !this.destroyRef.destroyed && generation === this.historyLoadGeneration;

    if (this.offlineQueue.isOnline()) {
      this.historyRequest = this.http
        .get<PaginatedResult<DriverTaskItem>>(
          `/api/dispatch/my-tasks?activeOnly=false&page=${page}&pageSize=${this.historyPageSize}`,
        )
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (data) => {
            if (!isCurrent()) return;
            this.historyTasks.set(data.items || []);
            this.historyTotalCount.set(data.totalCount || 0);
            this.historyTotalPages.set(data.totalPages || 1);
            this.historyLoading.set(false);
          },
          error: () => {
            if (!isCurrent()) return;
            this.historyLoading.set(false);
          },
        });
    } else {
      this.historyLoading.set(false);
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

  openDetails(task: DriverTaskItem): void {
    this.selectedTaskId.set(task.id);
    this.taskDialog.nativeElement.showModal();
    this.loadDetails();
  }

  loadDetails(): void {
    const id = this.selectedTaskId();
    if (!id) return;
    this.detailRequest?.unsubscribe();
    this.taskDetail.set(null);
    this.detailError.set(null);
    this.detailLoading.set(true);
    this.detailRequest = this.http
      .get<DriverTaskDetail>(`/api/dispatch/tasks/${encodeURIComponent(id)}`)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (detail) => {
          this.taskDetail.set(detail);
          this.detailLoading.set(false);
        },
        error: (error: unknown) => {
          this.detailError.set(this.userErrors.format(error));
          this.detailLoading.set(false);
        },
      });
  }

  closeDetails(): void {
    this.detailRequest?.unsubscribe();
    this.selectedTaskId.set(null);
    this.taskDetail.set(null);
    this.detailError.set(null);
    this.detailLoading.set(false);
    if (this.taskDialog.nativeElement.open) this.taskDialog.nativeElement.close();
  }

  async updateTaskStatus(
    task: DriverTaskItem,
    nextStatus: 'ACKNOWLEDGED' | 'IN_PROGRESS' | 'COMPLETED',
  ): Promise<void> {
    // An older cache/read must not undo this newly queued optimistic update.
    ++this.activeLoadGeneration;
    this.activeRequest?.unsubscribe();
    this.activeLoading.set(false);
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
