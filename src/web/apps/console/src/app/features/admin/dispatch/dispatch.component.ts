import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule, SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { AuthService } from '../../../core/auth/auth.service';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import {
  AreaOption,
  DispatchService,
  DriverOption,
  VehicleOption,
} from './services/dispatch.service';
import {
  JobTaskAlertDto,
  JobTaskDetailDto,
  JobTaskFilter,
  JobTaskStatus,
  TaskPriority,
} from './models/dispatch.models';

export type ViewState = 'loading' | 'success' | 'empty' | 'error' | 'forbidden';

@Component({
  selector: 'nim-dispatch',
  standalone: true,
  imports: [CommonModule, FormsModule, I18nPipe, LocaleDatePipe, SlicePipe, IconComponent],
  templateUrl: './dispatch.component.html',
  styleUrl: './dispatch.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DispatchComponent implements OnInit {
  private readonly dispatchService = inject(DispatchService);
  private readonly realtime = inject(RealtimeService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);

  // States
  readonly state = signal<ViewState>('loading');
  readonly errorMessage = signal<string>('');
  readonly isSubmitting = signal<boolean>(false);
  readonly formError = signal<string | null>(null);
  readonly areaWarning = signal<string | null>(null);

  // Data
  readonly tasks = signal<JobTaskDetailDto[]>([]);
  readonly alerts = signal<JobTaskAlertDto[]>([]);
  readonly drivers = signal<DriverOption[]>([]);
  readonly vehicles = signal<VehicleOption[]>([]);
  readonly areas = signal<AreaOption[]>([]);

  // Filters & Pagination
  readonly searchTerm = signal<string>('');
  readonly selectedStatus = signal<string>('');
  readonly selectedAreaId = signal<string>('');
  readonly selectedDriverId = signal<string>('');
  readonly selectedVehicleId = signal<string>('');
  readonly currentPage = signal<number>(1);
  readonly pageSize = signal<number>(20);
  readonly totalCount = signal<number>(0);
  readonly totalPages = signal<number>(1);

  // Modals
  readonly isCreateModalOpen = signal<boolean>(false);
  readonly isAssignModalOpen = signal<boolean>(false);
  readonly isStartModalOpen = signal<boolean>(false);
  readonly isCompleteModalOpen = signal<boolean>(false);
  readonly isCancelModalOpen = signal<boolean>(false);
  readonly isDetailsModalOpen = signal<boolean>(false);
  readonly selectedTask = signal<JobTaskDetailDto | null>(null);

  // Form State Models
  createForm = {
    ref: '',
    title: '',
    areaId: '',
    scheduledFor: '',
    priority: 'Medium' as TaskPriority,
    description: '',
    plannedDistanceKm: null as number | null,
    driverId: '',
    vehicleId: '',
    overrideAreaWarning: false,
  };

  assignForm = {
    driverId: '',
    vehicleId: '',
    scheduledFor: '',
    overrideAreaWarning: false,
  };

  startForm = {
    startedAt: '',
    startOdometerKm: null as number | null,
  };

  completeForm = {
    completedAt: '',
    actualDistanceKm: null as number | null,
    endOdometerKm: null as number | null,
  };

  cancelReason = '';

  ngOnInit(): void {
    if (!this.auth.isAdminOrDispatcher()) {
      this.state.set('forbidden');
      return;
    }

    this.loadLookups();
    this.loadTasks();
    this.loadAlerts();

    // SignalR Realtime Invalidation Subscription
    this.realtime.invalidation$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        // Upon receiving invalidation signal, trigger HTTP query reload
        this.loadTasks(false);
        this.loadAlerts();
      });
  }

  loadTasks(setLoadingState = true): void {
    if (setLoadingState) {
      this.state.set('loading');
    }
    this.errorMessage.set('');

    const filter: JobTaskFilter = {
      searchTerm: this.searchTerm() || undefined,
      status: (this.selectedStatus() as JobTaskStatus) || undefined,
      areaId: this.selectedAreaId() || undefined,
      driverId: this.selectedDriverId() || undefined,
      vehicleId: this.selectedVehicleId() || undefined,
      page: this.currentPage(),
      pageSize: this.pageSize(),
    };

    this.dispatchService.getTasks(filter).subscribe({
      next: (res) => {
        this.tasks.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
        this.totalPages.set(res.totalPages || 1);

        if (!res.items || res.items.length === 0) {
          this.state.set('empty');
        } else {
          this.state.set('success');
        }
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 403) {
          this.state.set('forbidden');
        } else {
          this.state.set('error');
          this.errorMessage.set(err.error?.message || err.message || 'Error loading tasks');
        }
      },
    });
  }

  loadAlerts(): void {
    this.dispatchService.getUnacknowledgedAlerts(30).subscribe({
      next: (alerts) => this.alerts.set(alerts || []),
      error: () => this.alerts.set([]),
    });
  }

  loadLookups(): void {
    this.dispatchService.getDrivers().subscribe({
      next: (res) => this.drivers.set(res.items || []),
      error: () => {},
    });
    this.dispatchService.getVehicles().subscribe({
      next: (res) => this.vehicles.set(res.items || []),
      error: () => {},
    });
    this.dispatchService.getAreas().subscribe({
      next: (res) => this.areas.set(res.items || []),
      error: () => {},
    });
  }

  onSearchInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.searchTerm.set(input.value);
    this.currentPage.set(1);
    this.loadTasks();
  }

  onStatusChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedStatus.set(select.value);
    this.currentPage.set(1);
    this.loadTasks();
  }

  onAreaChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedAreaId.set(select.value);
    this.currentPage.set(1);
    this.loadTasks();
  }

  onDriverChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedDriverId.set(select.value);
    this.currentPage.set(1);
    this.loadTasks();
  }

  onVehicleChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedVehicleId.set(select.value);
    this.currentPage.set(1);
    this.loadTasks();
  }

  clearFilters(): void {
    this.searchTerm.set('');
    this.selectedStatus.set('');
    this.selectedAreaId.set('');
    this.selectedDriverId.set('');
    this.selectedVehicleId.set('');
    this.currentPage.set(1);
    this.loadTasks();
  }

  filterByAssigned(): void {
    this.selectedStatus.set('Assigned');
    this.currentPage.set(1);
    this.loadTasks();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.loadTasks();
  }

  isTaskOverdueUnacknowledged(task: JobTaskDetailDto): boolean {
    if (task.status !== 'Assigned') return false;
    return this.alerts().some((a) => a.taskId === task.id);
  }

  getStatusKey(status: string): string {
    switch (status) {
      case 'Draft':
        return 'DRAFT';
      case 'Assigned':
        return 'ASSIGNED';
      case 'Acknowledged':
        return 'ACKNOWLEDGED';
      case 'InProgress':
        return 'IN_PROGRESS';
      case 'Completed':
        return 'COMPLETED';
      case 'Cancelled':
        return 'CANCELLED';
      default:
        return status.toUpperCase();
    }
  }

  // --- Modals & Actions ---

  openCreateModal(): void {
    const now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    const defaultScheduled = now.toISOString().slice(0, 16);

    this.createForm = {
      ref: '',
      title: '',
      areaId: this.areas().length > 0 ? this.areas()[0].id : '',
      scheduledFor: defaultScheduled,
      priority: 'Medium',
      description: '',
      plannedDistanceKm: null,
      driverId: '',
      vehicleId: '',
      overrideAreaWarning: false,
    };
    this.formError.set(null);
    this.areaWarning.set(null);
    this.isCreateModalOpen.set(true);
    this.onAreaOrDriverChangeInCreate();
  }

  closeCreateModal(): void {
    this.isCreateModalOpen.set(false);
  }

  onAreaOrDriverChangeInCreate(): void {
    if (!this.createForm.driverId || !this.createForm.areaId || !this.createForm.scheduledFor) {
      this.areaWarning.set(null);
      return;
    }
    const scheduledDate = this.createForm.scheduledFor.slice(0, 10);
    this.dispatchService
      .checkAreaEligibility(this.createForm.driverId, this.createForm.areaId, scheduledDate)
      .subscribe({
        next: (res) => {
          if (res.requiresWarning) {
            this.areaWarning.set(res.warningMessage || 'Area eligibility warning');
          } else {
            this.areaWarning.set(null);
          }
        },
        error: () => this.areaWarning.set(null),
      });
  }

  submitCreateTask(): void {
    if (!this.createForm.title || !this.createForm.areaId || !this.createForm.scheduledFor) {
      return;
    }
    this.isSubmitting.set(true);
    this.formError.set(null);

    const payload = {
      ref: this.createForm.ref || undefined,
      title: this.createForm.title,
      areaId: this.createForm.areaId,
      scheduledFor: new Date(this.createForm.scheduledFor).toISOString(),
      priority: this.createForm.priority,
      description: this.createForm.description || undefined,
      plannedDistanceKm: this.createForm.plannedDistanceKm ?? undefined,
      driverId: this.createForm.driverId || undefined,
      vehicleId: this.createForm.vehicleId || undefined,
      overrideAreaWarning: this.createForm.overrideAreaWarning,
    };

    this.dispatchService.createTask(payload).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.closeCreateModal();
        this.loadTasks();
      },
      error: (err: HttpErrorResponse) => {
        this.isSubmitting.set(false);
        this.formError.set(err.error?.message || err.error?.detail || err.message || 'Failed to create task');
      },
    });
  }

  openAssignModal(task: JobTaskDetailDto): void {
    this.selectedTask.set(task);
    const now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    const defaultScheduled = task.scheduledFor
      ? new Date(task.scheduledFor).toISOString().slice(0, 16)
      : now.toISOString().slice(0, 16);

    this.assignForm = {
      driverId: task.driverId || '',
      vehicleId: task.vehicleId || '',
      scheduledFor: defaultScheduled,
      overrideAreaWarning: false,
    };
    this.formError.set(null);
    this.areaWarning.set(null);
    this.isAssignModalOpen.set(true);
  }

  closeAssignModal(): void {
    this.isAssignModalOpen.set(false);
    this.selectedTask.set(null);
  }

  checkAssignEligibility(): void {
    const task = this.selectedTask();
    if (!task || !this.assignForm.driverId) {
      this.areaWarning.set(null);
      return;
    }
    const scheduledDate = (this.assignForm.scheduledFor || task.scheduledFor).slice(0, 10);
    this.dispatchService
      .checkAreaEligibility(this.assignForm.driverId, task.areaId, scheduledDate)
      .subscribe({
        next: (res) => {
          if (res.requiresWarning) {
            this.areaWarning.set(res.warningMessage || 'Area eligibility warning');
          } else {
            this.areaWarning.set(null);
          }
        },
        error: () => this.areaWarning.set(null),
      });
  }

  submitAssignTask(): void {
    const task = this.selectedTask();
    if (!task || !this.assignForm.driverId || !this.assignForm.vehicleId) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.dispatchService
      .assignTask(task.id, {
        driverId: this.assignForm.driverId,
        vehicleId: this.assignForm.vehicleId,
        scheduledFor: this.assignForm.scheduledFor
          ? new Date(this.assignForm.scheduledFor).toISOString()
          : undefined,
        overrideAreaWarning: this.assignForm.overrideAreaWarning,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeAssignModal();
          this.loadTasks();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.formError.set(err.error?.message || err.error?.detail || err.message || 'Failed to assign task');
        },
      });
  }

  acknowledgeTask(task: JobTaskDetailDto): void {
    this.dispatchService.acknowledgeTask(task.id, { acknowledgedAt: new Date().toISOString() }).subscribe({
      next: () => this.loadTasks(),
      error: (err: HttpErrorResponse) => {
        alert(err.error?.message || err.error?.detail || 'Failed to acknowledge task');
      },
    });
  }

  openStartModal(task: JobTaskDetailDto): void {
    this.selectedTask.set(task);
    const now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());

    this.startForm = {
      startedAt: now.toISOString().slice(0, 16),
      startOdometerKm: task.startOdometerKm ?? null,
    };
    this.formError.set(null);
    this.isStartModalOpen.set(true);
  }

  closeStartModal(): void {
    this.isStartModalOpen.set(false);
    this.selectedTask.set(null);
  }

  submitStartTask(): void {
    const task = this.selectedTask();
    if (!task) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.dispatchService
      .startTask(task.id, {
        startedAt: this.startForm.startedAt ? new Date(this.startForm.startedAt).toISOString() : undefined,
        startOdometerKm: this.startForm.startOdometerKm ?? undefined,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeStartModal();
          this.loadTasks();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.formError.set(err.error?.message || err.error?.detail || 'Failed to start task');
        },
      });
  }

  openCompleteModal(task: JobTaskDetailDto): void {
    this.selectedTask.set(task);
    const now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());

    this.completeForm = {
      completedAt: now.toISOString().slice(0, 16),
      actualDistanceKm: task.actualDistanceKm ?? task.plannedDistanceKm ?? null,
      endOdometerKm: task.endOdometerKm ?? null,
    };
    this.formError.set(null);
    this.isCompleteModalOpen.set(true);
  }

  closeCompleteModal(): void {
    this.isCompleteModalOpen.set(false);
    this.selectedTask.set(null);
  }

  submitCompleteTask(): void {
    const task = this.selectedTask();
    if (!task) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.dispatchService
      .completeTask(task.id, {
        completedAt: this.completeForm.completedAt
          ? new Date(this.completeForm.completedAt).toISOString()
          : undefined,
        actualDistanceKm: this.completeForm.actualDistanceKm ?? undefined,
        endOdometerKm: this.completeForm.endOdometerKm ?? undefined,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeCompleteModal();
          this.loadTasks();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.formError.set(err.error?.message || err.error?.detail || 'Failed to complete task');
        },
      });
  }

  openCancelModal(task: JobTaskDetailDto): void {
    this.selectedTask.set(task);
    this.cancelReason = '';
    this.formError.set(null);
    this.isCancelModalOpen.set(true);
  }

  closeCancelModal(): void {
    this.isCancelModalOpen.set(false);
    this.selectedTask.set(null);
  }

  submitCancelTask(): void {
    const task = this.selectedTask();
    if (!task || !this.cancelReason.trim()) return;

    this.isSubmitting.set(true);
    this.formError.set(null);

    this.dispatchService
      .cancelTask(task.id, {
        reason: this.cancelReason.trim(),
        cancelledAt: new Date().toISOString(),
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeCancelModal();
          this.loadTasks();
        },
        error: (err: HttpErrorResponse) => {
          this.isSubmitting.set(false);
          this.formError.set(err.error?.message || err.error?.detail || 'Failed to cancel task');
        },
      });
  }

  openDetailsModal(task: JobTaskDetailDto): void {
    this.selectedTask.set(task);
    this.isDetailsModalOpen.set(true);
  }

  closeDetailsModal(): void {
    this.isDetailsModalOpen.set(false);
    this.selectedTask.set(null);
  }
}
