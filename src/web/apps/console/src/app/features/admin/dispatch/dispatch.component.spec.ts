import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError, Subject } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { DispatchComponent } from './dispatch.component';
import { DispatchService } from './services/dispatch.service';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { JobTaskDetailDto, PaginatedResult } from './models/dispatch.models';
import { RealtimeMessage } from '../../../core/models/realtime.models';

declare const process: { env: Record<string, string | undefined> };

describe('DispatchComponent', () => {
  let component: DispatchComponent;
  let fixture: ComponentFixture<DispatchComponent>;
  let dispatchServiceMock: {
    getTasks: ReturnType<typeof vi.fn>;
    getUnacknowledgedAlerts: ReturnType<typeof vi.fn>;
    getDrivers: ReturnType<typeof vi.fn>;
    getVehicles: ReturnType<typeof vi.fn>;
    getAreas: ReturnType<typeof vi.fn>;
    createTask: ReturnType<typeof vi.fn>;
    assignTask: ReturnType<typeof vi.fn>;
    acknowledgeTask: ReturnType<typeof vi.fn>;
    startTask: ReturnType<typeof vi.fn>;
    completeTask: ReturnType<typeof vi.fn>;
    cancelTask: ReturnType<typeof vi.fn>;
    getTaskById: ReturnType<typeof vi.fn>;
    checkAreaEligibility: ReturnType<typeof vi.fn>;
  };
  let authServiceMock: {
    isAdminOrDispatcher: ReturnType<typeof vi.fn>;
    isAdmin: ReturnType<typeof vi.fn>;
  };
  let invalidationSubject: Subject<RealtimeMessage>;

  const mockTasks: JobTaskDetailDto[] = [
    {
      id: 'task-1',
      ref: 'TSK-001',
      title: 'Auckland CBD Freight',
      areaId: 'area-1',
      areaName: 'Auckland Central',
      areaCode: 'AKL-CBD',
      driverId: 'drv-1',
      driverName: 'John Driver',
      vehicleId: 'veh-1',
      vehicleRego: 'ABC123',
      scheduledFor: '2026-09-04T08:00:00Z',
      priority: 'High',
      status: 'Assigned',
      createdByUserId: 'usr-1',
      plannedDistanceKm: 25.5,
    },
    {
      id: 'task-2',
      ref: 'TSK-002',
      title: 'Manukau Parcel Run',
      areaId: 'area-2',
      areaName: 'Manukau Metro',
      areaCode: 'MNK-01',
      scheduledFor: '2026-09-04T09:30:00Z',
      priority: 'Medium',
      status: 'Draft',
      createdByUserId: 'usr-1',
      plannedDistanceKm: 42.0,
    },
  ];

  const mockPaginatedResult: PaginatedResult<JobTaskDetailDto> = {
    items: mockTasks,
    page: 1,
    pageSize: 20,
    totalCount: 2,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
  };

  beforeEach(async () => {
    invalidationSubject = new Subject<RealtimeMessage>();

    dispatchServiceMock = {
      getTasks: vi.fn().mockReturnValue(of(mockPaginatedResult)),
      getUnacknowledgedAlerts: vi.fn().mockReturnValue(of([])),
      getDrivers: vi.fn().mockReturnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 1, hasPreviousPage: false, hasNextPage: false })),
      getVehicles: vi.fn().mockReturnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 100, totalPages: 1, hasPreviousPage: false, hasNextPage: false })),
      getAreas: vi.fn().mockReturnValue(of({ items: [{ id: 'area-1', name: 'Auckland Central', code: 'AKL-CBD', isActive: true }], totalCount: 1, page: 1, pageSize: 100, totalPages: 1, hasPreviousPage: false, hasNextPage: false })),
      createTask: vi.fn().mockReturnValue(of(mockTasks[0])),
      assignTask: vi.fn().mockReturnValue(of(mockTasks[0])),
      acknowledgeTask: vi.fn().mockReturnValue(of(mockTasks[0])),
      startTask: vi.fn().mockReturnValue(of(mockTasks[0])),
      completeTask: vi.fn().mockReturnValue(of(mockTasks[0])),
      cancelTask: vi.fn().mockReturnValue(of(mockTasks[0])),
      getTaskById: vi.fn().mockReturnValue(of(mockTasks[0])),
      checkAreaEligibility: vi.fn().mockReturnValue(of({ isAssignedToArea: true, requiresWarning: false })),
    };

    authServiceMock = {
      isAdminOrDispatcher: vi.fn().mockReturnValue(true),
      isAdmin: vi.fn().mockReturnValue(true),
    };

    const realtimeServiceMock = {
      invalidation$: invalidationSubject.asObservable(),
    };

    await TestBed.configureTestingModule({
      imports: [DispatchComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: DispatchService, useValue: dispatchServiceMock },
        { provide: AuthService, useValue: authServiceMock },
        { provide: RealtimeService, useValue: realtimeServiceMock },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DispatchComponent);
    component = fixture.componentInstance;
  });

  it('should render dispatch tasks list in success state', () => {
    fixture.detectChanges();

    expect(component.state()).toBe('success');
    expect(component.tasks().length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.data-table')).toBeTruthy();
    expect(compiled.querySelectorAll('tbody tr').length).toBe(2);
    expect(compiled.textContent).toContain('Auckland CBD Freight');
    expect(compiled.textContent).toContain('TSK-001');
  });

  it('should render empty state when no tasks match query', () => {
    dispatchServiceMock.getTasks.mockReturnValue(
      of({
        items: [],
        page: 1,
        pageSize: 20,
        totalCount: 0,
        totalPages: 1,
        hasPreviousPage: false,
        hasNextPage: false,
      }),
    );

    fixture.detectChanges();

    expect(component.state()).toBe('empty');
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.state-card')).toBeTruthy();
    expect(compiled.querySelector('.state-title')).toBeTruthy();
  });

  it('should trigger search filtering and update page', () => {
    fixture.detectChanges();

    component.onSearchInput({ target: { value: 'Freight' } } as unknown as Event);

    expect(component.searchTerm()).toBe('Freight');
    expect(component.currentPage()).toBe(1);
    expect(dispatchServiceMock.getTasks).toHaveBeenCalledWith(
      expect.objectContaining({ searchTerm: 'Freight' }),
    );
  });

  it('should render explicit error state on API failure with retry button', () => {
    const errorResponse = new HttpErrorResponse({
      status: 500,
      statusText: 'Internal Server Error',
      error: { message: 'Database connection failed' },
    });
    dispatchServiceMock.getTasks.mockReturnValue(throwError(() => errorResponse));

    fixture.detectChanges();

    expect(component.state()).toBe('error');
    expect(component.errorMessage()).toContain('Database connection failed');

    const compiled = fixture.nativeElement as HTMLElement;
    const retryBtn = compiled.querySelector('.state-card button') as HTMLButtonElement;
    expect(retryBtn).toBeTruthy();

    // Reset to success and click retry
    dispatchServiceMock.getTasks.mockReturnValue(of(mockPaginatedResult));
    retryBtn.click();
    expect(dispatchServiceMock.getTasks).toHaveBeenCalledTimes(2);
  });

  it('should validate and submit create task form', () => {
    fixture.detectChanges();

    component.openCreateModal();
    expect(component.isCreateModalOpen()).toBe(true);

    component.createForm.title = 'New Delivery Task';
    component.createForm.areaId = 'area-1';
    component.createForm.scheduledFor = '2026-09-05T10:00';
    component.createForm.plannedDistanceKm = 18.5;

    component.submitCreateTask();

    expect(dispatchServiceMock.createTask).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'New Delivery Task',
        areaId: 'area-1',
        plannedDistanceKm: 18.5,
      }),
    );
    expect(component.isCreateModalOpen()).toBe(false);
  });

  it('rejects non-positive planned distance (0 or -1) on task creation (BUG-15)', () => {
    fixture.detectChanges();
    component.openCreateModal();

    component.createForm.title = 'Invalid Distance Task';
    component.createForm.areaId = 'area-1';
    component.createForm.scheduledFor = '2026-09-05T10:00';
    component.createForm.plannedDistanceKm = 0;

    component.submitCreateTask();
    expect(component.formError()).toBe('Planned distance must be greater than zero.');
    expect(dispatchServiceMock.createTask).not.toHaveBeenCalled();

    component.createForm.plannedDistanceKm = -5;
    component.submitCreateTask();
    expect(component.formError()).toBe('Planned distance must be greater than zero.');
    expect(dispatchServiceMock.createTask).not.toHaveBeenCalled();
  });

  it('captures structured ProblemDetails validation errors when creating task', () => {
    fixture.detectChanges();
    component.openCreateModal();

    component.createForm.title = 'Test Task';
    component.createForm.areaId = 'area-1';
    component.createForm.scheduledFor = '2026-09-05T10:00';
    component.createForm.plannedDistanceKm = 10;

    dispatchServiceMock.createTask.mockReturnValue(
      throwError(() => ({
        status: 400,
        error: {
          type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: {
            PlannedDistanceKm: ['Planned distance must be greater than zero.'],
          },
        },
      })),
    );

    component.submitCreateTask();
    expect(component.formError()).toBe('Planned distance must be greater than zero.');
  });

  it('fetches full task details and renders cancellation reason and timestamp in details modal (BUG-16)', () => {
    const cancelledDetail: JobTaskDetailDto = {
      ...mockTasks[0],
      status: 'Cancelled',
      cancelledAt: '2026-09-05T14:30:00Z',
      cancellationReason: 'Customer requested reschedule',
    };
    dispatchServiceMock.getTaskById.mockReturnValue(of(cancelledDetail));

    fixture.detectChanges();
    component.openDetailsModal(mockTasks[0]);
    fixture.detectChanges();

    expect(dispatchServiceMock.getTaskById).toHaveBeenCalledWith('task-1');
    expect(component.selectedTask()?.cancelledAt).toBe('2026-09-05T14:30:00Z');
    expect(component.selectedTask()?.cancellationReason).toBe('Customer requested reschedule');

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Customer requested reschedule');
  });

  it('should automatically reload data via HTTP when SignalR invalidation signal arrives (Realtime AC)', () => {
    fixture.detectChanges();
    expect(dispatchServiceMock.getTasks).toHaveBeenCalledTimes(1);

    // Simulate SignalR receiving invalidation message { Kind, EntityId, OccurredAt }
    invalidationSubject.next({
      kind: 'TaskAcknowledged',
      entityId: 'task-1',
      occurredAt: new Date().toISOString(),
    });

    // Verify HTTP endpoint was re-queried for authoritative data
    expect(dispatchServiceMock.getTasks).toHaveBeenCalledTimes(2);
    expect(dispatchServiceMock.getUnacknowledgedAlerts).toHaveBeenCalledTimes(2);
  });

  describe('Timezone & Wall-Clock Echo Tests (AC 1, AC 2, R1)', () => {
    const originalTz = process.env['TZ'];

    afterEach(() => {
      if (originalTz !== undefined) {
        process.env['TZ'] = originalTz;
      } else {
        delete process.env['TZ'];
      }
    });

    it('submits create task and echoes in assign modal with matching wall-clock in Pacific/Auckland (UTC+12)', () => {
      process.env['TZ'] = 'Pacific/Auckland';

      fixture.detectChanges();
      component.openCreateModal();

      // User creates task at 17:59 local wall-clock
      component.createForm.title = 'Evening Freight';
      component.createForm.areaId = 'area-1';
      component.createForm.scheduledFor = '2026-09-07T17:59';

      component.submitCreateTask();

      // In UTC+12, 17:59 NZST is 05:59 UTC
      expect(dispatchServiceMock.createTask).toHaveBeenCalledWith(
        expect.objectContaining({
          scheduledFor: '2026-09-07T05:59:00.000Z',
        }),
      );

      // Server returns task with UTC scheduledFor
      const createdTask: JobTaskDetailDto = {
        ...mockTasks[0],
        id: 'task-new',
        scheduledFor: '2026-09-07T05:59:00.000Z',
      };

      // User opens assign modal for the created task
      component.openAssignModal(createdTask);

      // Assign modal MUST echo 17:59 (NOT 05:59)
      expect(component.assignForm.scheduledFor).toBe('2026-09-07T17:59');

      // User submits assign task without editing scheduled time
      component.assignForm.driverId = 'drv-1';
      component.assignForm.vehicleId = 'veh-1';
      component.submitAssignTask();

      expect(dispatchServiceMock.assignTask).toHaveBeenCalledWith(
        'task-new',
        expect.objectContaining({
          scheduledFor: '2026-09-07T05:59:00.000Z',
        }),
      );
    });

    it('preserves exact wall-clock across NZ Daylight Saving Time (DST) boundaries (AC 2)', () => {
      process.env['TZ'] = 'Pacific/Auckland';
      fixture.detectChanges();

      // 1. Before DST (NZST = UTC+12): Sat 26 Sep 2026 18:00
      const preDstTask: JobTaskDetailDto = {
        ...mockTasks[0],
        id: 'task-pre-dst',
        scheduledFor: '2026-09-26T06:00:00.000Z',
      };
      component.openAssignModal(preDstTask);
      expect(component.assignForm.scheduledFor).toBe('2026-09-26T18:00');

      // 2. After DST (NZDT = UTC+13): Mon 28 Sep 2026 18:00
      const postDstTask: JobTaskDetailDto = {
        ...mockTasks[0],
        id: 'task-post-dst',
        scheduledFor: '2026-09-28T05:00:00.000Z',
      };
      component.openAssignModal(postDstTask);
      expect(component.assignForm.scheduledFor).toBe('2026-09-28T18:00');

      // Submitting assign form on post-DST date correctly uses UTC+13 (05:00 UTC)
      component.assignForm.driverId = 'drv-1';
      component.assignForm.vehicleId = 'veh-1';
      component.submitAssignTask();

      expect(dispatchServiceMock.assignTask).toHaveBeenCalledWith(
        'task-post-dst',
        expect.objectContaining({
          scheduledFor: '2026-09-28T05:00:00.000Z',
        }),
      );
    });
  });
});

