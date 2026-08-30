import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DriverPayslipsComponent } from './driver-payslips.component';
import { OfflineCacheService } from '../../../core/offline/offline-cache.service';
import { OfflineQueueService } from '../../../core/offline/offline-queue.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { FormatService } from '../../../core/i18n/format.service';

describe('DriverPayslipsComponent (Offline view & currency/date formatting)', () => {
  let component: DriverPayslipsComponent;
  let httpMock: HttpTestingController;

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

    const fixture = TestBed.createComponent(DriverPayslipsComponent);
    component = fixture.componentInstance;
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loads payslips successfully', () => {
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
    expect(component.payslips()[0].netPay).toBe(1450.0);
  });
});
