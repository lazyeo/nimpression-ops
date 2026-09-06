import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DriverTasksComponent, DriverTaskItem } from './driver-tasks.component';
import { OfflineCacheService } from '../../../core/offline/offline-cache.service';
import { OfflineQueueService } from '../../../core/offline/offline-queue.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { RealtimeService } from '../../../core/realtime/realtime.service';

describe('DriverTasksComponent (Offline Cached View & Touch Targets)', () => {
  let component: DriverTasksComponent;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [DriverTasksComponent],
      providers: [
        I18nService,
        OfflineCacheService,
        OfflineQueueService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(DriverTasksComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loads active tasks by default with activeOnly=true and displays them', () => {
    const mockTasks: DriverTaskItem[] = [
      {
        id: 't-1',
        tripNo: 'TRIP-101',
        status: 'ASSIGNED',
        pickupLocation: 'Auckland Port',
        deliveryLocation: 'Manukau Depot',
        scheduledTime: '2026-08-24T08:00:00Z',
        vehiclePlate: 'NIM-888',
      },
    ];

    const req = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=true&pageSize=50');
    req.flush({
      items: mockTasks,
      totalCount: 1,
      page: 1,
      pageSize: 50,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });

    expect(component.activeTab()).toBe('active');
    expect(component.tasks().length).toBe(1);
    expect(component.tasks()[0].tripNo).toBe('TRIP-101');
  });

  it('switches to history tab and performs server-side pagination with activeOnly=false', () => {
    // Flush initial active tasks request
    const initReq = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=true&pageSize=50');
    initReq.flush({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 50,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });

    // Create mock tasks for page 1 (5 items) and page 2 (2 items)
    const mockPage1: DriverTaskItem[] = Array.from({ length: 5 }, (_, i) => ({
      id: `hist-${i + 1}`,
      tripNo: `HIST-${100 + i + 1}`,
      status: i % 2 === 0 ? 'COMPLETED' : 'CANCELLED',
      pickupLocation: `Pickup ${i + 1}`,
      deliveryLocation: `Delivery ${i + 1}`,
      scheduledTime: '2026-08-20T08:00:00Z',
      vehiclePlate: `NIM-${100 + i}`,
    }));

    const mockPage2: DriverTaskItem[] = Array.from({ length: 2 }, (_, i) => ({
      id: `hist-${i + 6}`,
      tripNo: `HIST-${100 + i + 6}`,
      status: 'COMPLETED',
      pickupLocation: `Pickup ${i + 6}`,
      deliveryLocation: `Delivery ${i + 6}`,
      scheduledTime: '2026-08-20T08:00:00Z',
      vehiclePlate: `NIM-${105 + i}`,
    }));

    // Switch to history tab -> triggers server request for page 1
    component.setTab('history');
    expect(component.activeTab()).toBe('history');

    const histReq1 = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=false&page=1&pageSize=5');
    histReq1.flush({
      items: mockPage1,
      totalCount: 7,
      page: 1,
      pageSize: 5,
      totalPages: 2,
      hasPreviousPage: false,
      hasNextPage: true,
    });

    expect(component.historyTasks().length).toBe(5);
    expect(component.historyTotalCount()).toBe(7);
    expect(component.historyTotalPages()).toBe(2);
    expect(component.historyPage()).toBe(1);
    expect(component.historyTasks()[0].tripNo).toBe('HIST-101');

    // Go to next page -> triggers server request for page 2
    component.nextHistoryPage();
    const histReq2 = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=false&page=2&pageSize=5');
    histReq2.flush({
      items: mockPage2,
      totalCount: 7,
      page: 2,
      pageSize: 5,
      totalPages: 2,
      hasPreviousPage: true,
      hasNextPage: false,
    });

    expect(component.historyPage()).toBe(2);
    expect(component.historyTasks().length).toBe(2);
    expect(component.historyTasks()[0].tripNo).toBe('HIST-106');

    // Go back to previous page -> triggers server request for page 1
    component.prevHistoryPage();
    const histReq3 = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=false&page=1&pageSize=5');
    histReq3.flush({
      items: mockPage1,
      totalCount: 7,
      page: 1,
      pageSize: 5,
      totalPages: 2,
      hasPreviousPage: false,
      hasNextPage: true,
    });

    expect(component.historyPage()).toBe(1);
    expect(component.historyTasks().length).toBe(5);
  });

  it('R2 Regression: completing a task in active tab then switching to history tab still makes backend request and fetches full history', async () => {
    const activeItem: DriverTaskItem = {
      id: 't-1',
      tripNo: 'TRIP-101',
      status: 'IN_PROGRESS',
      pickupLocation: 'Auckland Port',
      deliveryLocation: 'Manukau Depot',
      scheduledTime: '2026-08-24T08:00:00Z',
      vehiclePlate: 'NIM-888',
    };

    // Initial load in active tab
    const initReq = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=true&pageSize=50');
    initReq.flush({
      items: [activeItem],
      totalCount: 1,
      page: 1,
      pageSize: 50,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });

    expect(component.tasks().length).toBe(1);

    // 1. Complete the task in active tab
    await component.updateTaskStatus(activeItem, 'COMPLETED');
    expect(component.tasks().length).toBe(0);

    const postReq = httpMock.expectOne('/api/dispatch/tasks/t-1/status');
    expect(postReq.request.body).toEqual({ status: 'COMPLETED' });
    postReq.flush({});

    // 2. Switch to history tab
    component.setTab('history');
    expect(component.activeTab()).toBe('history');

    // Assert: Backend request MUST be issued even though a task was completed
    const histReq = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=false&page=1&pageSize=5');
    const mockFullHistory: DriverTaskItem[] = [
      { ...activeItem, status: 'COMPLETED' },
      {
        id: 'hist-old-1',
        tripNo: 'HIST-001',
        status: 'COMPLETED',
        pickupLocation: 'Depot A',
        deliveryLocation: 'Depot B',
        scheduledTime: '2026-08-20T08:00:00Z',
        vehiclePlate: 'NIM-101',
      },
    ];

    histReq.flush({
      items: mockFullHistory,
      totalCount: 15,
      page: 1,
      pageSize: 5,
      totalPages: 3,
      hasPreviousPage: false,
      hasNextPage: true,
    });

    expect(component.historyTasks().length).toBe(2);
    expect(component.historyTotalCount()).toBe(15);
    expect(component.historyTotalPages()).toBe(3);
    expect(component.historyTasks()[0].tripNo).toBe('TRIP-101');
  });

  it('re-queries API when SignalR invalidation signal arrives for driver task', async () => {
    const initialTasks: DriverTaskItem[] = [
      {
        id: 't-1',
        tripNo: 'TRIP-101',
        status: 'ASSIGNED',
        pickupLocation: 'Auckland Port',
        deliveryLocation: 'Manukau Depot',
        scheduledTime: '2026-08-24T08:00:00Z',
        vehiclePlate: 'NIM-888',
      },
    ];

    const initialReq = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=true&pageSize=50');
    initialReq.flush({
      items: initialTasks,
      totalCount: 1,
      page: 1,
      pageSize: 50,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });
    expect(component.tasks().length).toBe(1);

    const realtime = TestBed.inject(RealtimeService);
    // Simulate incoming task.assigned invalidation signal
    (realtime as any).invalidationSubject.next({
      kind: 'task.assigned',
      entityId: 't-2',
      occurredAt: new Date().toISOString(),
    });

    // Wait for async offlineCache read to complete
    await new Promise((resolve) => setTimeout(resolve, 10));

    const reloadReq = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=true&pageSize=50');
    reloadReq.flush({
      items: [
        ...initialTasks,
        {
          id: 't-2',
          tripNo: 'TRIP-102',
          status: 'ASSIGNED',
          pickupLocation: 'Airport',
          deliveryLocation: 'CBD',
          scheduledTime: '2026-08-24T10:00:00Z',
          vehiclePlate: 'NIM-999',
        },
      ],
      totalCount: 2,
      page: 1,
      pageSize: 50,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });

    expect(component.tasks().length).toBe(2);
    expect(component.tasks()[1].tripNo).toBe('TRIP-102');
  });
});
