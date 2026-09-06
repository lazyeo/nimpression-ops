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

  it('loads tasks and displays them', () => {
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

    const req = httpMock.expectOne('/api/dispatch/my-tasks');
    req.flush(mockTasks);

    expect(component.tasks().length).toBe(1);
    expect(component.tasks()[0].tripNo).toBe('TRIP-101');
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

    const initialReq = httpMock.expectOne('/api/dispatch/my-tasks');
    initialReq.flush(initialTasks);
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

    const reloadReq = httpMock.expectOne('/api/dispatch/my-tasks');
    reloadReq.flush([
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
    ]);

    expect(component.tasks().length).toBe(2);
    expect(component.tasks()[1].tripNo).toBe('TRIP-102');
  });
});
