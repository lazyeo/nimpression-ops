import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DriverShiftsComponent, ShiftStatusDto } from './driver-shifts.component';
import { OfflineCacheService } from '../../../core/offline/offline-cache.service';
import { OfflineQueueService } from '../../../core/offline/offline-queue.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { RealtimeService } from '../../../core/realtime/realtime.service';

describe('DriverShiftsComponent (Clock in/out & Shifts)', () => {
  let component: DriverShiftsComponent;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [DriverShiftsComponent],
      providers: [
        I18nService,
        OfflineCacheService,
        OfflineQueueService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(DriverShiftsComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('handles clock in state update', async () => {
    await new Promise((resolve) => setTimeout(resolve, 20));
    // Handle initial load in ngOnInit
    const initialReq = httpMock.expectOne('/api/timesheet/current-shift');
    initialReq.flush({ status: 'NOT_STARTED', totalWorkedMinutes: 0 });

    expect(component.currentShift().status).toBe('NOT_STARTED');
    await component.clockIn();
    const clockInReq = httpMock.expectOne('/api/timesheet/clock-in');
    clockInReq.flush({ success: true });
    expect(component.currentShift().status).toBe('ACTIVE');
  });

  it('re-queries shift API when SignalR invalidation arrives for shift/timesheet', async () => {
    await new Promise((resolve) => setTimeout(resolve, 20));
    const initialReq = httpMock.expectOne('/api/timesheet/current-shift');
    initialReq.flush({ status: 'NOT_STARTED', totalWorkedMinutes: 0 });

    const realtime = TestBed.inject(RealtimeService);
    (realtime as any).invalidationSubject.next({
      kind: 'shift.started',
      entityId: 'shift-1',
      occurredAt: new Date().toISOString(),
    });

    await new Promise((resolve) => setTimeout(resolve, 20));

    const reloadReq = httpMock.expectOne('/api/timesheet/current-shift');
    reloadReq.flush({
      id: 'shift-1',
      status: 'ACTIVE',
      clockedInAt: '2026-09-06T08:00:00Z',
      totalWorkedMinutes: 120,
    });

    expect(component.currentShift().status).toBe('ACTIVE');
    expect(component.currentShift().totalWorkedMinutes).toBe(120);
  });
});
