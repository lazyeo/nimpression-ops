import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { PayrollComponent } from './payroll.component';
import { PayrollService } from './services/payroll.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { FormatService } from '../../../core/i18n/format.service';
import { PayPeriodDto, PayslipDto } from './models/payroll.models';

describe('PayrollComponent', () => {
  let component: PayrollComponent;
  let fixture: ComponentFixture<PayrollComponent>;
  let payrollService: any;

  const mockPeriods: PayPeriodDto[] = [
    {
      id: 'period-1',
      startsOn: '2026-09-01',
      endsOn: '2026-09-14',
      status: 'Calculating',
      finalisedAt: null,
      paidAt: null,
      payslipCount: 5,
    },
    {
      id: 'period-2',
      startsOn: '2026-08-18',
      endsOn: '2026-08-31',
      status: 'Finalised',
      finalisedAt: '2026-09-01T10:00:00Z',
      paidAt: '2026-09-02T12:00:00Z',
      payslipCount: 5,
    },
  ];

  const mockPayslip: PayslipDto = {
    id: 'payslip-1',
    payPeriodId: 'period-1',
    periodStartsOn: '2026-09-01',
    periodEndsOn: '2026-09-14',
    driverId: 'driver-1',
    driverName: 'Alice Cooper',
    employeeNo: 'DRV007',
    ordinaryHours: 80,
    overtimeHours: 10,
    holidayHours: 0,
    hourlyRateSnapshot: 25.0,
    hoursBasedGross: 2375.0,
    completedTripCount: 45,
    totalDistanceKm: 1200.0,
    perTripRateSnapshot: 20.0,
    perKmRateSnapshot: 0.8,
    tripBasedGross: 1860.0,
    basisUsed: 'Hourly',
    grossPay: 2375.0,
    currency: 'NZD',
    minimumWageTopUp: true,
    calculatedAt: '2026-09-14T18:00:00Z',
    finalisedAt: null,
    lines: [
      {
        id: 'line-1',
        basis: 'Hourly',
        kind: 'Ordinary',
        description: '80.00 Ordinary Hours @ $25.00/hr',
        rate: 25.0,
        currency: 'NZD',
        amount: 2000.0,
        hours: 80,
      },
    ],
    shiftDetails: [
      {
        shiftId: 'shift-101',
        clockInAt: '2026-09-01T08:00:00Z',
        clockOutAt: '2026-09-01T17:00:00Z',
        breakMinutes: 30,
        attributedDate: '2026-09-01',
        payableHours: 8.5,
      },
    ],
    tripDetails: [
      {
        jobTaskId: 'task-201',
        ref: 'TSK-9901',
        title: 'Depot to Airport Delivery',
        completedAt: '2026-09-01T14:30:00Z',
        effectiveDistanceKm: 32.5,
      },
    ],
    fines: [
      {
        fineId: 'fine-1',
        reference: 'NZTA-7788',
        issuedOn: '2026-09-03',
        authority: 'NZTA',
        amount: 150.0,
        currency: 'NZD',
        status: 'Accepted',
        reason: 'Speeding 10km/h over limit',
      },
    ],
    finesLegalNotice:
      'Under the Wages Protection Act 1983, employer deductions from pay are unlawful without prior written consent.',
  };

  beforeEach(async () => {
    payrollService = {
      getPayPeriods: vi.fn().mockReturnValue(
        of({
          items: mockPeriods,
          totalCount: 2,
          page: 1,
          pageSize: 20,
          totalPages: 1,
        }),
      ),
      createPayPeriod: vi.fn().mockReturnValue(of(mockPeriods[0])),
      calculatePayroll: vi.fn().mockReturnValue(of(void 0)),
      finalisePayPeriod: vi.fn().mockReturnValue(of(void 0)),
      voidPayPeriod: vi.fn().mockReturnValue(of(void 0)),
      getPayPeriodPayslips: vi.fn().mockReturnValue(of([mockPayslip])),
      getPayslipById: vi.fn().mockReturnValue(of(mockPayslip)),
    };

    await TestBed.configureTestingModule({
      imports: [PayrollComponent],
      providers: [
        I18nService,
        FormatService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PayrollService, useValue: payrollService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PayrollComponent);
    component = fixture.componentInstance;
  });

  it('renders pay periods list on initial load (List rendering test)', () => {
    fixture.detectChanges();

    expect(component.payPeriods().length).toBe(2);
    expect(component.isLoading()).toBe(false);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('2026-09-01 ~ 2026-09-14');
    expect(compiled.textContent).toContain('2026-08-18 ~ 2026-08-31');
  });

  it('renders empty data state when no pay periods exist (Empty state test)', () => {
    payrollService.getPayPeriods.mockReturnValue(
      of({ items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 }),
    );

    component.loadPayPeriods();
    fixture.detectChanges();

    expect(component.payPeriods().length).toBe(0);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.empty-state')).toBeTruthy();
  });

  it('filters pay periods by status and date range (Filter test)', () => {
    component.selectedStatus.set('Finalised');
    component.fromDate.set('2026-08-01');
    component.toDate.set('2026-08-31');

    component.applyFilters();
    fixture.detectChanges();

    expect(payrollService.getPayPeriods).toHaveBeenCalledWith(
      expect.objectContaining({
        status: 'Finalised',
        fromDate: '2026-08-01',
        toDate: '2026-08-31',
        page: 1,
        pageSize: 20,
      }),
    );
  });

  it('handles error state properly when API call fails (Error state test)', () => {
    payrollService.getPayPeriods.mockReturnValue(
      throwError(() => ({ status: 500, message: 'Database failure' })),
    );

    component.loadPayPeriods();
    fixture.detectChanges();

    expect(component.hasError()).toBe(true);
    expect(component.isLoading()).toBe(false);
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.error-state')).toBeTruthy();
  });

  it('opens create dialog and submits new pay period (Form action test)', () => {
    component.openCreateModal();
    expect(component.isCreateModalOpen()).toBe(true);

    component.newStartsOn = '2026-09-15';
    component.newEndsOn = '2026-09-28';
    component.submitCreatePeriod();

    expect(payrollService.createPayPeriod).toHaveBeenCalledWith({
      startsOn: '2026-09-15',
      endsOn: '2026-09-28',
    });
  });

  it('renders dual-basis breakdown, BasisUsed settlement banner, minimum wage topup, and separate fines partition in payslip detail modal (Dual-basis and compliance test)', () => {
    component.viewPayslipDetail(mockPayslip);
    fixture.detectChanges();

    expect(component.isDetailModalOpen()).toBe(true);
    expect(component.activePayslip()).toBeTruthy();

    const compiled = fixture.nativeElement as HTMLElement;

    // Dual-basis: both Hours-based and Trip-based cards exist in DOM
    const basisCards = compiled.querySelectorAll('.basis-card');
    expect(basisCards.length).toBe(2);

    // BasisUsed settlement
    expect(component.activePayslip()?.basisUsed).toBe('Hourly');
    expect(compiled.querySelector('.settlement-hero')).toBeTruthy();

    // Minimum Wage Protection notice
    expect(compiled.querySelector('.min-wage-banner')).toBeTruthy();

    // Separate fines partition with NZ Wages Protection Act notice
    expect(compiled.querySelector('.fines-partition')).toBeTruthy();
    expect(compiled.querySelector('.zero-deduction-badge')).toBeTruthy();
  });
});
