import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { TimesheetsComponent } from './timesheets.component';
import { TimesheetsService } from './services/timesheets.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { FormatService } from '../../../core/i18n/format.service';
import { RealtimeService } from '../../../core/realtime/realtime.service';
import { ShiftEntryDto, TimesheetSummaryDto } from './models/timesheets.models';

describe('TimesheetsComponent', () => {
  let component: TimesheetsComponent;
  let fixture: ComponentFixture<TimesheetsComponent>;
  let timesheetsService: any;

  const mockShifts: ShiftEntryDto[] = [
    {
      id: 'shift-1',
      driverId: 'driver-1',
      driverName: 'Alice Driver',
      clockInAt: '2026-09-01T08:00:00Z',
      clockOutAt: '2026-09-01T17:00:00Z',
      locationUnavailable: false,
      breakMinutes: 30,
      payableHours: 8.5,
      status: 'Completed',
      attributedDate: '2026-09-01',
    },
    {
      id: 'shift-2',
      driverId: 'driver-2',
      driverName: 'Bob Driver',
      clockInAt: '2026-09-01T09:00:00Z',
      clockOutAt: null,
      locationUnavailable: false,
      breakMinutes: 0,
      payableHours: 4.0,
      status: 'Active',
      attributedDate: '2026-09-01',
    },
  ];

  const mockSummary: TimesheetSummaryDto = {
    fromDate: '2026-09-01',
    toDate: '2026-09-07',
    totalShifts: 2,
    totalPayableHours: 12.5,
    totalOrdinaryHours: 12.5,
    totalOvertimeHours: 0,
    totalBreakMinutes: 30,
    dailySummaries: [],
  };

  beforeEach(async () => {
    timesheetsService = {
      getTimesheets: vi.fn().mockReturnValue(
        of({
          items: mockShifts,
          totalCount: 2,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        }),
      ),
      getTimesheetSummary: vi.fn().mockReturnValue(of(mockSummary)),
      getDrivers: vi.fn().mockReturnValue(
        of({
          items: [
            { id: 'driver-1', displayName: 'Alice Driver', employeeNo: 'DRV001' },
            { id: 'driver-2', displayName: 'Bob Driver', employeeNo: 'DRV002' },
          ],
        }),
      ),
      adminCorrectShift: vi.fn().mockReturnValue(of(mockShifts[0])),
    };

    await TestBed.configureTestingModule({
      imports: [TimesheetsComponent],
      providers: [
        I18nService,
        FormatService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: TimesheetsService, useValue: timesheetsService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(TimesheetsComponent);
    component = fixture.componentInstance;
  });

  it('renders shifts list and displays summary KPIs on initial load (List rendering test)', () => {
    fixture.detectChanges();

    expect(component.timesheets().length).toBe(2);
    expect(component.summary()?.totalShifts).toBe(2);
    expect(component.summary()?.totalPayableHours).toBe(12.5);
    expect(component.isLoading()).toBe(false);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Alice Driver');
    expect(compiled.textContent).toContain('Bob Driver');
  });

  it('renders empty data state when no shifts exist (Empty state test)', () => {
    timesheetsService.getTimesheets.mockReturnValue(
      of({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 }),
    );

    component.loadTimesheets();
    fixture.detectChanges();

    expect(component.timesheets().length).toBe(0);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.empty-state')).toBeTruthy();
  });

  it('applies filters with driver, status, and date range (Filter test)', () => {
    component.selectedDriverId.set('driver-1');
    component.selectedStatus.set('Completed');
    component.fromDate.set('2026-09-01');
    component.toDate.set('2026-09-01');

    component.applyFilters();
    fixture.detectChanges();

    expect(timesheetsService.getTimesheets).toHaveBeenCalledWith(
      expect.objectContaining({
        driverId: 'driver-1',
        status: 'Completed',
        fromDate: '2026-09-01',
        toDate: '2026-09-01',
        page: 1,
        pageSize: 20,
      }),
    );
  });

  it('handles error state properly with retry option (Error state test)', () => {
    timesheetsService.getTimesheets.mockReturnValue(
      throwError(() => ({ status: 500, message: 'Server error' })),
    );

    component.loadTimesheets();
    fixture.detectChanges();

    expect(component.hasError()).toBe(true);
    expect(component.isLoading()).toBe(false);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.error-state')).toBeTruthy();
  });

  it('enforces mandatory correction reason when submitting admin correction (Validation & audit test)', () => {
    component.openCorrectModal(mockShifts[0]);
    expect(component.isCorrectModalOpen()).toBe(true);

    component.correctionReason = '';
    component.submitCorrection();

    expect(component.correctionError()).toBe('Correction reason is mandatory.');
    expect(timesheetsService.adminCorrectShift).not.toHaveBeenCalled();

    component.correctionReason = 'DriverForgotToClockOut';
    component.submitCorrection();
    expect(timesheetsService.adminCorrectShift).toHaveBeenCalled();
  });

  it('should automatically reload timesheets and summary when SignalR invalidation signal arrives', () => {
    fixture.detectChanges();
    expect(timesheetsService.getTimesheets).toHaveBeenCalledTimes(1);
    expect(timesheetsService.getTimesheetSummary).toHaveBeenCalledTimes(1);

    const realtime = TestBed.inject(RealtimeService);
    (realtime as any).invalidationSubject.next({
      kind: 'shift.completed',
      entityId: 'shift-1',
      occurredAt: new Date().toISOString(),
    });

    expect(timesheetsService.getTimesheets).toHaveBeenCalledTimes(2);
    expect(timesheetsService.getTimesheetSummary).toHaveBeenCalledTimes(2);
  });
});
