import enDictionary from '../../../../assets/i18n/en-NZ.json';
import zhDictionary from '../../../../assets/i18n/zh-CN.json';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DriverPayslipsComponent } from './driver-payslips.component';
import { OfflineCacheService } from '../../../core/offline/offline-cache.service';
import { OfflineQueueService } from '../../../core/offline/offline-queue.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { FormatService } from '../../../core/i18n/format.service';
import { RealtimeService } from '../../../core/realtime/realtime.service';

declare const process: { env: Record<string, string | undefined> };

describe('DriverPayslipsComponent (Offline view & currency/date formatting)', () => {
  let fixture: ComponentFixture<DriverPayslipsComponent>;
  let component: DriverPayslipsComponent;
  let httpMock: HttpTestingController;
  const originalTz = process.env['TZ'];

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [DriverPayslipsComponent],
      providers: [
        I18nService,
        FormatService,
        OfflineCacheService,
        OfflineQueueService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    TestBed.inject(I18nService).setDictionary('en-NZ', enDictionary);
    TestBed.inject(I18nService).setDictionary('zh-CN', zhDictionary);
    TestBed.inject(I18nService).currentLang.set('en-NZ');

    fixture = TestBed.createComponent(DriverPayslipsComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    if (originalTz !== undefined) {
      process.env['TZ'] = originalTz;
    } else {
      delete process.env['TZ'];
    }
  });

  it('treats legacy pay amounts without calculation status as unavailable', () => {
    const req = httpMock.expectOne('/api/payroll/my-payslips');
    req.flush([
      {
        id: 'ps-1',
        payPeriod: '2026-W34',
        payDate: '2026-08-25',
        grossPay: 1850.0,
        netPay: 1450.0,
        deductions: 400.0,
        totalHours: 42.5,
        hourlyRate: 35.0,
        currency: 'NZD',
      },
    ]);

    expect(component.payslips().length).toBe(1);
    expect(component.payslips()[0].netPay).toBeNull();
    expect(component.payslips()[0].deductions).toBeNull();
    expect(component.payslips()[0].hourlyRate).toBeNull();
  });

  it.each([
    ['en-NZ', 'Gross Pay', 'Deductions not calculated'],
    ['zh-CN', '税前工资', '尚未计算扣款'],
  ] as const)(
    'shows gross pay and explicit unavailable deductions in %s',
    (language, grossLabel, deductionLabel) => {
      TestBed.inject(I18nService).currentLang.set(language);
      httpMock.expectOne('/api/payroll/my-payslips').flush([
        {
          id: 'uncalculated',
          periodStartsOn: '2026-09-01',
          periodEndsOn: '2026-09-14',
          grossPay: 2800,
          netPay: null,
          deductions: null,
          deductionCalculationStatus: 'NotCalculated',
          totalHours: 80,
          hourlyRate: 0,
          currency: 'NZD',
        },
      ]);
      fixture.detectChanges();
      const element = fixture.nativeElement as HTMLElement;
      expect(element.querySelector('.net-label')?.textContent).toContain(grossLabel);
      expect(element.querySelector('.net-amount')?.textContent).toContain('2,800.00');
      expect(element.textContent).toContain(deductionLabel);
      expect(element.querySelector('.deduction-val')).toBeNull();
      expect(element.querySelector('.period-val')?.textContent).toContain(
        '01/09/2026 ~ 14/09/2026',
      );
      expect(component.payslips()[0].hourlyRate).toBe(0);
    },
  );

  it('shows net pay only when the response explicitly includes a calculated settlement', () => {
    httpMock.expectOne('/api/payroll/my-payslips').flush([
      {
        id: 'calculated',
        periodStartsOn: '2026-09-01',
        periodEndsOn: '2026-09-14',
        grossPay: 2800,
        netPay: 2800,
        deductions: 0,
        deductionCalculationStatus: 'Calculated',
        totalHours: 80,
        hourlyRate: 35,
        currency: 'NZD',
      },
    ]);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.net-label')?.textContent).toContain('Net Pay');
    expect(element.querySelector('.deduction-val')?.textContent).toContain('0.00');
    expect(element.querySelector('.calculation-note')).toBeNull();
  });

  it('does not reuse invented net pay or guessed rate from an older offline cache', async () => {
    httpMock.expectOne('/api/payroll/my-payslips').flush([]);
    vi.spyOn(TestBed.inject(OfflineCacheService), 'getDriverPayslips').mockResolvedValue([
      {
        id: 'legacy',
        payPeriod: '2026-09-01 ~ 2026-09-14',
        grossPay: 2800,
        netPay: 2800,
        deductions: 0,
        hourlyRate: 35,
        totalHours: 80,
        currency: 'NZD',
      },
    ]);
    component.loadPayslips();
    await Promise.resolve();
    httpMock
      .expectOne('/api/payroll/my-payslips')
      .flush('Unavailable', { status: 503, statusText: 'Unavailable' });
    fixture.detectChanges();
    expect(component.isUsingCache()).toBe(true);
    expect(component.payslips()[0].netPay).toBeNull();
    expect(component.payslips()[0].hourlyRate).toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Deductions not calculated',
    );
    expect((fixture.nativeElement as HTMLElement).querySelector('.deduction-val')).toBeNull();
  });

  it('loads and sanitizes the cache on a cold offline initial load', async () => {
    httpMock.expectOne('/api/payroll/my-payslips').flush([]);
    vi.spyOn(TestBed.inject(OfflineQueueService), 'isOnline').mockReturnValue(false);
    vi.spyOn(TestBed.inject(OfflineCacheService), 'getDriverPayslips').mockResolvedValue([
      {
        id: 'legacy-offline',
        grossPay: 800,
        netPay: 800,
        deductions: 0,
        hourlyRate: 35,
        payPeriod: '2026-09-01 ~ 2026-09-14',
        totalHours: 20,
        currency: 'NZD',
      },
    ]);
    const offlineFixture = TestBed.createComponent(DriverPayslipsComponent);
    offlineFixture.detectChanges();
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();
    offlineFixture.detectChanges();
    expect(offlineFixture.componentInstance.payslips()[0].id).toBe('legacy-offline');
    expect(offlineFixture.componentInstance.payslips()[0].netPay).toBeNull();
    expect(offlineFixture.componentInstance.isLoading()).toBe(false);
    expect(offlineFixture.nativeElement.textContent).toContain('Deductions not calculated');
    httpMock.expectNone('/api/payroll/my-payslips');
    offlineFixture.destroy();
  });

  it('does not let a late cache overwrite fresh network data', async () => {
    httpMock.expectOne('/api/payroll/my-payslips').flush([]);
    let resolveCache!: (value: unknown[]) => void;
    vi.spyOn(TestBed.inject(OfflineCacheService), 'getDriverPayslips').mockReturnValue(
      new Promise((resolve) => {
        resolveCache = resolve;
      }),
    );
    component.loadPayslips();
    httpMock.expectOne('/api/payroll/my-payslips').flush([]);
    resolveCache([{ id: 'stale', grossPay: 800 }]);
    await Promise.resolve();
    expect(component.payslips()).toEqual([]);
    expect(component.isUsingCache()).toBe(false);
  });

  it('cancels its request and ignores pending cache data after destruction', async () => {
    httpMock.expectOne('/api/payroll/my-payslips').flush([]);
    let resolveCache!: (value: unknown[]) => void;
    vi.spyOn(TestBed.inject(OfflineCacheService), 'getDriverPayslips').mockReturnValue(
      new Promise((resolve) => {
        resolveCache = resolve;
      }),
    );
    component.loadPayslips();
    const pending = httpMock.expectOne('/api/payroll/my-payslips');
    fixture.destroy();
    expect(pending.cancelled).toBe(true);
    resolveCache([{ id: 'after-destroy', grossPay: 800 }]);
    await Promise.resolve();
    expect(component.payslips()).toEqual([]);
  });

  it('refreshes payslips after reconnection', () => {
    httpMock.expectOne('/api/payroll/my-payslips').flush([]);
    (
      TestBed.inject(RealtimeService) as unknown as {
        invalidationSubject: { next(value: unknown): void };
      }
    ).invalidationSubject.next({
      kind: 'realtime.reconnected',
      occurredAt: '2026-09-15T00:00:00Z',
    });
    httpMock.expectOne('/api/payroll/my-payslips').flush([]);
    expect(component.isUsingCache()).toBe(false);
  });

  it('re-queries payslips API when SignalR invalidation arrives for payslip', async () => {
    const initialReq = httpMock.expectOne('/api/payroll/my-payslips');
    initialReq.flush([
      {
        id: 'ps-1',
        payPeriod: '2026-W34',
        payDate: '2026-08-25',
        grossPay: 1850.0,
        netPay: 1450.0,
        deductions: 400.0,
        totalHours: 42.5,
        hourlyRate: 35.0,
        currency: 'NZD',
      },
    ]);

    const realtime = TestBed.inject(RealtimeService);
    (realtime as any).invalidationSubject.next({
      kind: 'payslip.finalised',
      entityId: 'ps-2',
      occurredAt: '2026-09-15T00:00:00Z',
    });

    const reloadReq = httpMock.expectOne('/api/payroll/my-payslips');
    reloadReq.flush([
      {
        id: 'ps-1',
        payPeriod: '2026-W34',
        payDate: '2026-08-25',
        grossPay: 1850.0,
        netPay: 1450.0,
        deductions: 400.0,
        totalHours: 42.5,
        hourlyRate: 35.0,
        currency: 'NZD',
      },
      {
        id: 'ps-2',
        payPeriod: '2026-W35',
        payDate: '2026-09-01',
        grossPay: 1950.0,
        netPay: 1550.0,
        deductions: 400.0,
        totalHours: 45.0,
        hourlyRate: 35.0,
        currency: 'NZD',
      },
    ]);

    expect(component.payslips().length).toBe(2);
    expect(component.payslips()[1].id).toBe('ps-2');
  });

  it('renders correctly when payDate is null for unpaid pay period', () => {
    const req = httpMock.expectOne('/api/payroll/my-payslips');
    req.flush([
      {
        id: 'ps-unpaid',
        payPeriod: '2026-08-09 ~ 2026-08-22',
        payDate: null,
        grossPay: 2340.0,
        netPay: 2340.0,
        deductions: 0.0,
        totalHours: 68.5,
        hourlyRate: 34.0,
        currency: 'NZD',
      },
    ]);

    expect(component.payslips().length).toBe(1);
    expect(component.payslips()[0].payDate).toBeNull();
  });

  it('keeps isUsingCache false when online request successfully loads data (BUG-11)', () => {
    const req = httpMock.expectOne('/api/payroll/my-payslips');
    req.flush([
      {
        id: 'ps-1',
        payPeriod: '2026-W34',
        payDate: '2026-08-25',
        grossPay: 1850.0,
        netPay: 1450.0,
        deductions: 400.0,
        totalHours: 42.5,
        hourlyRate: 35.0,
        currency: 'NZD',
      },
    ]);

    expect(component.isUsingCache()).toBe(false);
    expect(component.isLoading()).toBe(false);
  });

  it('sets isUsingCache true when online request fails (W20 fallback pattern)', () => {
    const req = httpMock.expectOne('/api/payroll/my-payslips');
    req.flush('Server error', { status: 500, statusText: 'Internal Server Error' });

    expect(component.isUsingCache()).toBe(true);
    expect(component.isLoading()).toBe(false);
  });

  describe('Timezone-aware payDate rendering (W35 / BUG-12)', () => {
    it('renders morning payments on the exact same date as admin in Pacific/Auckland (AC 1)', () => {
      process.env['TZ'] = 'Pacific/Auckland';

      const formatService = TestBed.inject(FormatService);
      // Payment made on 11 Aug 2026 at 11:30 AM NZST (which is 10 Aug 2026 23:30:00 UTC)
      const rawPaidAtIso = '2026-08-10T23:30:00.000Z';

      const req = httpMock.expectOne('/api/payroll/my-payslips');
      req.flush([
        {
          id: 'ps-morning-1',
          payPeriod: '2026-07-26 ~ 2026-08-08',
          payDate: rawPaidAtIso,
          grossPay: 2100.0,
          netPay: 2100.0,
          deductions: 0.0,
          totalHours: 60.0,
          hourlyRate: 35.0,
          currency: 'NZD',
        },
      ]);
      fixture.detectChanges();

      const badge = fixture.nativeElement.querySelector('.pay-date-badge');
      expect(badge).toBeTruthy();

      const expectedFormattedDate = formatService.formatDate(rawPaidAtIso, 'short');
      // Admin side renders: {{ period.paidAt | localeDate: 'short' }}
      // Driver side renders: {{ 'DRIVER.PAY_DATE' | i18n }}: {{ slip.payDate | localeDate: 'short' }}
      expect(badge.textContent).toContain(expectedFormattedDate);
      expect(expectedFormattedDate).toContain('11');
      expect(expectedFormattedDate).toContain('8');
      expect(expectedFormattedDate).toContain('2026');
    });

    it('renders all 5 historical regression pay periods accurately in Pacific/Auckland (BUG-12)', () => {
      process.env['TZ'] = 'Pacific/Auckland';
      const formatService = TestBed.inject(FormatService);

      // 5 regression test cases from BUG-12 (NZ morning timestamps):
      // 1. 2026-08-11 10:00 NZST (UTC: 2026-08-10T22:00:00Z) -> 11/08
      // 2. 2026-07-28 09:30 NZST (UTC: 2026-07-27T21:30:00Z) -> 28/07
      // 3. 2026-07-14 11:00 NZST (UTC: 2026-07-13T23:00:00Z) -> 14/07
      // 4. 2026-06-30 08:45 NZST (UTC: 2026-06-29T20:45:00Z) -> 30/06
      // 5. 2026-06-16 10:15 NZST (UTC: 2026-06-15T22:15:00Z) -> 16/06
      const cases = [
        { iso: '2026-08-10T22:00:00.000Z', expectedDay: '11' },
        { iso: '2026-07-27T21:30:00.000Z', expectedDay: '28' },
        { iso: '2026-07-13T23:00:00.000Z', expectedDay: '14' },
        { iso: '2026-06-29T20:45:00.000Z', expectedDay: '30' },
        { iso: '2026-06-15T22:15:00.000Z', expectedDay: '16' },
      ];

      const req = httpMock.expectOne('/api/payroll/my-payslips');
      req.flush(
        cases.map((c, idx) => ({
          id: `ps-${idx}`,
          payPeriod: `Period-${idx}`,
          payDate: c.iso,
          grossPay: 1500,
          netPay: 1500,
          deductions: 0,
          totalHours: 40,
          hourlyRate: 35,
          currency: 'NZD',
        })),
      );
      fixture.detectChanges();

      const badges = fixture.nativeElement.querySelectorAll('.pay-date-badge');
      expect(badges.length).toBe(5);

      cases.forEach((c, idx) => {
        const expectedDate = formatService.formatDate(c.iso, 'short');
        expect(badges[idx].textContent).toContain(expectedDate);
        expect(expectedDate).toContain(c.expectedDay);
      });
    });

    it('does not display pay date badge when period is unpaid (payDate is null / AC 3 / W28 regression check)', () => {
      const req = httpMock.expectOne('/api/payroll/my-payslips');
      req.flush([
        {
          id: 'ps-unpaid-w28',
          payPeriod: '2026-08-09 ~ 2026-08-22',
          payDate: null,
          grossPay: 2340.0,
          netPay: 2340.0,
          deductions: 0.0,
          totalHours: 68.5,
          hourlyRate: 34.0,
          currency: 'NZD',
        },
      ]);
      fixture.detectChanges();

      const badge = fixture.nativeElement.querySelector('.pay-date-badge');
      expect(badge).toBeNull();
      expect(component.payslips()[0].payDate).toBeNull();
    });
  });
});
