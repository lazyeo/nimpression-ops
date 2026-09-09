import { Subject } from 'rxjs';
import { signal } from '@angular/core';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DriverTasksComponent, DriverTaskItem, DriverTaskDetail } from './driver-tasks.component';
import { OfflineCacheService } from '../../../core/offline/offline-cache.service';
import { OfflineQueueService } from '../../../core/offline/offline-queue.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { RealtimeService } from '../../../core/realtime/realtime.service';

describe('DriverTasksComponent (Offline Cached View & Touch Targets)', () => {
  let fixture: ComponentFixture<DriverTasksComponent>;
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

    fixture = TestBed.createComponent(DriverTasksComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loads active tasks by default with activeOnly=true and displays them with totalCount', () => {
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

    const req = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20');
    req.flush({
      items: mockTasks,
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });

    expect(component.activeTab()).toBe('active');
    expect(component.tasks().length).toBe(1);
    expect(component.activeTotalCount()).toBe(1);
    expect(component.tasks()[0].tripNo).toBe('TRIP-101');
  });

  it('R5 Requirement: paginates active tasks without silent truncation when totalCount exceeds pageSize', () => {
    // Initial load: 52 active tasks across 3 pages (pageSize=20)
    const mockActivePage1: DriverTaskItem[] = Array.from({ length: 20 }, (_, i) => ({
      id: `act-${i + 1}`,
      tripNo: `TRIP-${100 + i + 1}`,
      status: 'ASSIGNED',
      pickupLocation: `Pickup ${i + 1}`,
      deliveryLocation: `Delivery ${i + 1}`,
      scheduledTime: '2026-08-24T08:00:00Z',
      vehiclePlate: `NIM-${100 + i}`,
    }));

    const mockActivePage2: DriverTaskItem[] = Array.from({ length: 20 }, (_, i) => ({
      id: `act-${i + 21}`,
      tripNo: `TRIP-${100 + i + 21}`,
      status: 'IN_PROGRESS',
      pickupLocation: `Pickup ${i + 21}`,
      deliveryLocation: `Delivery ${i + 21}`,
      scheduledTime: '2026-08-24T09:00:00Z',
      vehiclePlate: `NIM-${120 + i}`,
    }));

    const initReq = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20');
    initReq.flush({
      items: mockActivePage1,
      totalCount: 52,
      page: 1,
      pageSize: 20,
      totalPages: 3,
      hasPreviousPage: false,
      hasNextPage: true,
    });

    expect(component.tasks().length).toBe(20);
    expect(component.activeTotalCount()).toBe(52);
    expect(component.activeTotalPages()).toBe(3);
    expect(component.activePage()).toBe(1);

    // Navigate to page 2 of active tasks
    component.nextActivePage();
    const page2Req = httpMock.expectOne(
      '/api/dispatch/my-tasks?activeOnly=true&page=2&pageSize=20',
    );
    page2Req.flush({
      items: mockActivePage2,
      totalCount: 52,
      page: 2,
      pageSize: 20,
      totalPages: 3,
      hasPreviousPage: true,
      hasNextPage: true,
    });

    expect(component.activePage()).toBe(2);
    expect(component.tasks().length).toBe(20);
    expect(component.tasks()[0].tripNo).toBe('TRIP-121');

    // Navigate back to page 1
    component.prevActivePage();
    const page1Req = httpMock.expectOne(
      '/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20',
    );
    page1Req.flush({
      items: mockActivePage1,
      totalCount: 52,
      page: 1,
      pageSize: 20,
      totalPages: 3,
      hasPreviousPage: false,
      hasNextPage: true,
    });

    expect(component.activePage()).toBe(1);
    expect(component.tasks().length).toBe(20);
    expect(component.tasks()[0].tripNo).toBe('TRIP-101');
  });

  it('switches to history tab and performs server-side pagination with activeOnly=false', () => {
    // Flush initial active tasks request
    const initReq = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20');
    initReq.flush({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
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

    const histReq1 = httpMock.expectOne(
      '/api/dispatch/my-tasks?activeOnly=false&page=1&pageSize=5',
    );
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
    const histReq2 = httpMock.expectOne(
      '/api/dispatch/my-tasks?activeOnly=false&page=2&pageSize=5',
    );
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
    const histReq3 = httpMock.expectOne(
      '/api/dispatch/my-tasks?activeOnly=false&page=1&pageSize=5',
    );
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
    const initReq = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20');
    initReq.flush({
      items: [activeItem],
      totalCount: 1,
      page: 1,
      pageSize: 20,
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

  it.each(['active', 'history'] as const)(
    'refreshes both task lists after reconnect while viewing %s',
    (tab) => {
      const activeUrl = '/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20';
      const historyUrl = '/api/dispatch/my-tasks?activeOnly=false&page=1&pageSize=5';
      const cancelled = { id: 'cancelled-task', tripNo: 'TRIP-CANCELLED', status: 'Cancelled' };
      const page = (items: unknown[]) => ({
        items,
        totalCount: items.length,
        page: 1,
        pageSize: 20,
        totalPages: 1,
      });
      httpMock.expectOne(activeUrl).flush(page([{ ...cancelled, status: 'Assigned' }]));
      if (tab === 'history') {
        component.setTab('history');
        httpMock.expectOne(historyUrl).flush(page([]));
      }
      const realtime = TestBed.inject(RealtimeService) as unknown as {
        invalidationSubject: Subject<{ kind: string; occurredAt: string }>;
      };
      realtime.invalidationSubject.next({
        kind: 'realtime.reconnected',
        occurredAt: '2026-09-09T00:00:00Z',
      });
      httpMock.expectOne(activeUrl).flush(page([]));
      httpMock.expectOne(historyUrl).flush(page([cancelled]));
      expect(component.tasks()).toEqual([]);
      expect(component.historyTasks()[0].status).toBe('Cancelled');
      expect(component.activeTab()).toBe(tab);
    },
  );

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

    const initialReq = httpMock.expectOne(
      '/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20',
    );
    initialReq.flush({
      items: initialTasks,
      totalCount: 1,
      page: 1,
      pageSize: 20,
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

    const reloadReq = httpMock.expectOne(
      '/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20',
    );
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
      pageSize: 20,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });

    expect(component.tasks().length).toBe(2);
    expect(component.tasks()[1].tripNo).toBe('TRIP-102');
  });

  it('AC 3: when task status is ASSIGNED, UI renders Accept action and must NOT render Start action', async () => {
    const assignedTask: DriverTaskItem = {
      id: 't-assigned-1',
      tripNo: 'TRIP-301',
      status: 'ASSIGNED',
      pickupLocation: 'Depot North',
      deliveryLocation: 'Depot South',
      scheduledTime: '2026-08-24T08:00:00Z',
      vehiclePlate: 'NIM-123',
    };

    const initReq = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20');
    initReq.flush({
      items: [assignedTask],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });

    fixture.detectChanges();

    const rootEl = fixture.nativeElement as HTMLElement;
    const acceptBtn = rootEl.querySelector('.btn-accept') as HTMLButtonElement;
    const startBtn = rootEl.querySelector('.btn-start') as HTMLButtonElement;
    const completeBtn = rootEl.querySelector('.btn-complete') as HTMLButtonElement;

    // Must render Accept button
    expect(acceptBtn).not.toBeNull();
    // Must NOT render Start or Complete button
    expect(startBtn).toBeNull();
    expect(completeBtn).toBeNull();

    // Clicking Accept updates status to ACKNOWLEDGED and enqueues status update
    acceptBtn.click();
    await new Promise((resolve) => setTimeout(resolve, 10));

    expect(component.tasks()[0].status).toBe('ACKNOWLEDGED');

    const postReq = httpMock.expectOne('/api/dispatch/tasks/t-assigned-1/status');
    expect(postReq.request.body).toEqual({ status: 'ACKNOWLEDGED' });
    postReq.flush({});
  });

  it('when task status is ACKNOWLEDGED, UI renders Start action (not Accept); clicking it transitions to IN_PROGRESS', async () => {
    const ackTask: DriverTaskItem = {
      id: 't-ack-1',
      tripNo: 'TRIP-302',
      status: 'ACKNOWLEDGED',
      pickupLocation: 'Depot North',
      deliveryLocation: 'Depot South',
      scheduledTime: '2026-08-24T08:00:00Z',
      vehiclePlate: 'NIM-123',
    };

    const initReq = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20');
    initReq.flush({
      items: [ackTask],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });

    fixture.detectChanges();

    const rootEl = fixture.nativeElement as HTMLElement;
    const acceptBtn = rootEl.querySelector('.btn-accept') as HTMLButtonElement;
    const startBtn = rootEl.querySelector('.btn-start') as HTMLButtonElement;
    const completeBtn = rootEl.querySelector('.btn-complete') as HTMLButtonElement;

    // Must render Start button
    expect(startBtn).not.toBeNull();
    // Must NOT render Accept or Complete button
    expect(acceptBtn).toBeNull();
    expect(completeBtn).toBeNull();

    // Clicking Start updates status to IN_PROGRESS and enqueues status update
    startBtn.click();
    await new Promise((resolve) => setTimeout(resolve, 10));

    expect(component.tasks()[0].status).toBe('IN_PROGRESS');

    const postReq = httpMock.expectOne('/api/dispatch/tasks/t-ack-1/status');
    expect(postReq.request.body).toEqual({ status: 'IN_PROGRESS' });
    postReq.flush({});
  });

  it('when task status is IN_PROGRESS, UI renders Complete action; clicking it transitions to COMPLETED', async () => {
    const inProgressTask: DriverTaskItem = {
      id: 't-prog-1',
      tripNo: 'TRIP-303',
      status: 'IN_PROGRESS',
      pickupLocation: 'Depot North',
      deliveryLocation: 'Depot South',
      scheduledTime: '2026-08-24T08:00:00Z',
      vehiclePlate: 'NIM-123',
    };

    const initReq = httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20');
    initReq.flush({
      items: [inProgressTask],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
      hasPreviousPage: false,
      hasNextPage: false,
    });

    fixture.detectChanges();

    const rootEl = fixture.nativeElement as HTMLElement;
    const acceptBtn = rootEl.querySelector('.btn-accept') as HTMLButtonElement;
    const startBtn = rootEl.querySelector('.btn-start') as HTMLButtonElement;
    const completeBtn = rootEl.querySelector('.btn-complete') as HTMLButtonElement;

    // Must render Complete button
    expect(completeBtn).not.toBeNull();
    // Must NOT render Accept or Start button
    expect(acceptBtn).toBeNull();
    expect(startBtn).toBeNull();

    // Clicking Complete removes from active tasks and enqueues status update
    completeBtn.click();
    await new Promise((resolve) => setTimeout(resolve, 10));

    expect(component.tasks().length).toBe(0);

    const postReq = httpMock.expectOne('/api/dispatch/tasks/t-prog-1/status');
    expect(postReq.request.body).toEqual({ status: 'COMPLETED' });
    postReq.flush({});
  });

  const detailTask: DriverTaskItem = {
    id: 'detail-task',
    tripNo: 'TRIP-DETAIL',
    status: 'COMPLETED',
    pickupLocation: 'List pickup',
    deliveryLocation: 'List summary',
    scheduledTime: '2026-09-09T00:00:00Z',
    vehiclePlate: 'OLD-PLATE',
  };
  const retrievedDetail: DriverTaskDetail = {
    id: 'detail-task',
    ref: 'TRIP-DETAIL',
    title: 'Chilled groceries for store delivery',
    description: '12 chilled cartons. Keep refrigerated.\nUnload at receiving bay 2.',
    areaName: 'Auckland Central',
    areaCode: 'AKL-CBD',
    vehicleRego: 'NIM001',
    scheduledFor: '2026-09-09T00:00:00Z',
    priority: 'High',
    status: 'Completed',
    acknowledgedAt: '2026-09-08T23:30:00Z',
    startedAt: '2026-09-09T00:00:00Z',
    completedAt: '2026-09-09T02:00:00Z',
    cancelledAt: null,
    cancellationReason: null,
    plannedDistanceKm: 24,
    actualDistanceKm: 26,
  };

  function stubNativeDialog(): HTMLDialogElement {
    const dialog: HTMLDialogElement = fixture.nativeElement.querySelector('dialog');
    // JSDOM does not implement native dialog focus/modal behavior; browser validation covers it.
    dialog.showModal = () => dialog.setAttribute('open', '');
    dialog.close = () => dialog.removeAttribute('open');
    return dialog;
  }

  it.each(['active', 'history'] as const)(
    'opens %s task details on demand using returned delivery notes',
    (tab) => {
      httpMock.expectOne('/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20').flush({
        items: tab === 'active' ? [{ ...detailTask, status: 'ASSIGNED' }] : [],
        totalCount: 1,
        totalPages: 1,
      });
      if (tab === 'history') {
        component.setTab('history');
        httpMock
          .expectOne('/api/dispatch/my-tasks?activeOnly=false&page=1&pageSize=5')
          .flush({ items: [detailTask], totalCount: 1, totalPages: 1 });
      }
      fixture.detectChanges();
      httpMock.expectNone('/api/dispatch/tasks/detail-task');
      const dialog = stubNativeDialog();
      const button: HTMLButtonElement = fixture.nativeElement.querySelector(
        '.task-cards-list .btn-details',
      );
      expect(button.getAttribute('aria-label')).toContain('TRIP-DETAIL');
      button.click();
      expect(dialog.open).toBe(true);
      expect(component.detailLoading()).toBe(true);
      httpMock.expectOne('/api/dispatch/tasks/detail-task').flush(retrievedDetail);
      fixture.detectChanges();
      expect(dialog.textContent).toContain('Chilled groceries for store delivery');
      expect(dialog.textContent).toContain('12 chilled cartons. Keep refrigerated.');
      expect(dialog.textContent).toContain('Unload at receiving bay 2.');
      expect(dialog.textContent).toContain('Auckland Central');
      expect(dialog.textContent).toContain('NIM001');
      expect(dialog.textContent).not.toContain('OLD-PLATE');
      expect(component.taskDetail()?.completedAt).toBe('2026-09-09T02:00:00Z');
      (dialog.querySelector('.detail-close') as HTMLButtonElement).click();
      expect(dialog.open).toBe(false);
      expect(component.taskDetail()).toBeNull();
    },
  );

  it('shows a safe access failure without retaining prior task content', () => {
    httpMock
      .expectOne('/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20')
      .flush({ items: [detailTask], totalCount: 1, totalPages: 1 });
    const dialog = stubNativeDialog();
    component.openDetails(detailTask);
    httpMock.expectOne('/api/dispatch/tasks/detail-task').flush(retrievedDetail);
    component.closeDetails();
    component.openDetails({ ...detailTask, id: 'other-task' });
    httpMock
      .expectOne('/api/dispatch/tasks/other-task')
      .flush(
        { title: 'forbidden', detail: 'Private backend endpoint /api/dispatch/tasks/other-task' },
        { status: 403, statusText: 'Forbidden' },
      );
    fixture.detectChanges();
    expect(dialog.textContent).toContain('OPS-403');
    expect(dialog.textContent).not.toContain('Private backend');
    expect(dialog.textContent).not.toContain('/api/dispatch');
    expect(dialog.textContent).not.toContain('12 chilled cartons');
    expect(component.taskDetail()).toBeNull();
  });

  it('cancels a pending detail request when the dialog closes', () => {
    httpMock
      .expectOne('/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20')
      .flush({ items: [], totalCount: 0, totalPages: 1 });
    stubNativeDialog();
    component.openDetails(detailTask);
    const pending = httpMock.expectOne('/api/dispatch/tasks/detail-task');
    component.closeDetails();
    expect(pending.cancelled).toBe(true);
    expect(component.selectedTaskId()).toBeNull();
    expect(component.detailLoading()).toBe(false);
  });
});

describe('DriverTasksComponent request ordering', () => {
  const activeUrl = '/api/dispatch/my-tasks?activeOnly=true&page=1&pageSize=20';
  const historyUrl = '/api/dispatch/my-tasks?activeOnly=false&page=1&pageSize=5';
  const staleTask: DriverTaskItem = {
    id: 'cancelled-task',
    tripNo: 'TRIP-CANCELLED',
    status: 'ASSIGNED',
    pickupLocation: 'Depot',
    deliveryLocation: 'Store',
    scheduledTime: '2026-09-09T00:00:00Z',
    vehiclePlate: 'NIM001',
  };
  const page = (items: DriverTaskItem[]) => ({ items, totalCount: items.length, totalPages: 1 });
  const deferredCache = () => {
    let resolve!: (items: DriverTaskItem[] | null) => void;
    const promise = new Promise<DriverTaskItem[] | null>((complete) => {
      resolve = complete;
    });
    return { promise, resolve };
  };
  let fixture: ComponentFixture<DriverTasksComponent>;
  let component: DriverTasksComponent;
  let http: HttpTestingController;
  let online: ReturnType<typeof signal<boolean>>;
  let invalidations: Subject<{ kind: string }>;
  let cacheReads: ReturnType<typeof deferredCache>[];

  beforeEach(async () => {
    online = signal(true);
    invalidations = new Subject();
    cacheReads = [];
    await TestBed.configureTestingModule({
      imports: [DriverTasksComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: OfflineQueueService,
          useValue: { isOnline: online, enqueue: vi.fn().mockResolvedValue(undefined) },
        },
        { provide: RealtimeService, useValue: { invalidation$: invalidations.asObservable() } },
        {
          provide: OfflineCacheService,
          useValue: {
            getDriverTasks: () => {
              const cache = deferredCache();
              cacheReads.push(cache);
              return cache.promise;
            },
            cacheDriverTasks: vi.fn().mockResolvedValue(undefined),
          },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(DriverTasksComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    fixture.destroy();
    http.verify();
  });

  it('renders a delayed cache on an offline cold start', async () => {
    online.set(false);
    fixture.detectChanges();
    http.expectNone(activeUrl);
    cacheReads[0].resolve([staleTask]);
    await cacheReads[0].promise;
    fixture.detectChanges();
    expect(component.tasks()).toEqual([staleTask]);
    expect(component.activeTotalCount()).toBe(1);
    expect(component.isUsingCache()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('TRIP-CANCELLED');
  });

  it('uses delayed fallback cache even when the HTTP request has already failed', async () => {
    fixture.detectChanges();
    http.expectOne(activeUrl).flush({}, { status: 503, statusText: 'Unavailable' });
    cacheReads[0].resolve([staleTask]);
    await cacheReads[0].promise;
    expect(component.tasks()).toEqual([staleTask]);
    expect(component.isLoading()).toBe(false);
    expect(component.isUsingCache()).toBe(true);
  });

  it('cancels the older GET and rejects all old cache reads after a cancellation refresh succeeds', async () => {
    fixture.detectChanges();
    const older = http.expectOne(activeUrl);
    invalidations.next({ kind: 'task.cancelled' });
    expect(older.cancelled).toBe(true);
    http.expectOne(activeUrl).flush(page([]));
    for (const cache of cacheReads) {
      cache.resolve([staleTask]);
      await cache.promise;
    }
    expect(component.tasks()).toEqual([]);
    expect(component.activeTotalCount()).toBe(0);
    expect(component.isUsingCache()).toBe(false);
    expect(() => older.flush(page([staleTask]))).toThrow();
  });

  it('keeps history loading separate from the active cache during reconnect', async () => {
    fixture.detectChanges();
    http.expectOne(activeUrl).flush(page([]));
    component.setTab('history');
    http.expectOne(historyUrl).flush(page([]));
    invalidations.next({ kind: 'realtime.reconnected' });
    http.expectOne(historyUrl).flush(page([{ ...staleTask, status: 'CANCELLED' }]));
    http.expectOne(activeUrl).flush({}, { status: 503, statusText: 'Unavailable' });
    cacheReads[1].resolve([staleTask]);
    await cacheReads[1].promise;
    expect(component.tasks()).toEqual([staleTask]);
    expect(component.activeTab()).toBe('history');
    expect(component.historyTasks()[0].status).toBe('CANCELLED');
    expect(component.isLoading()).toBe(false);
  });

  it('cancels obsolete history GETs and all list requests when destroyed', async () => {
    fixture.detectChanges();
    const active = http.expectOne(activeUrl);
    component.setTab('history');
    const oldHistory = http.expectOne(historyUrl);
    void component.loadHistory();
    const currentHistory = http.expectOne(historyUrl);
    expect(oldHistory.cancelled).toBe(true);
    fixture.destroy();
    expect(active.cancelled).toBe(true);
    expect(currentHistory.cancelled).toBe(true);
    cacheReads[0].resolve([staleTask]);
    await cacheReads[0].promise;
    expect(component.tasks()).toEqual([]);
  });
});
