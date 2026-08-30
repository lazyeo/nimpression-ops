import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApiClientService } from './api-client.service';

describe('ApiClientService', () => {
  let service: ApiClientService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ApiClientService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(ApiClientService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should fetch vehicles with query parameters', () => {
    service.getVehicles({ status: 'Active', pageSize: 50 }).subscribe(res => {
      expect(res.items).toHaveLength(1);
      expect(res.items[0].rego).toBe('ABC123');
    });

    const req = httpTesting.expectOne(r => r.url === '/api/vehicles' && r.params.get('status') === 'Active');
    expect(req.request.method).toBe('GET');
    req.flush({ items: [{ id: '1', rego: 'ABC123', status: 'Active' }], totalCount: 1, page: 1, pageSize: 50, totalPages: 1 });
  });

  it('should fetch job tasks with date range filters', () => {
    service.getJobTasks({ from: '2026-08-01T00:00:00Z', to: '2026-08-31T23:59:59Z' }).subscribe(res => {
      expect(res.items).toHaveLength(1);
    });

    const req = httpTesting.expectOne(r => r.url === '/api/dispatch/tasks' && r.params.has('from'));
    expect(req.request.method).toBe('GET');
    req.flush({ items: [{ id: 't1', title: 'Delivery Job', status: 'Completed' }], totalCount: 1, page: 1, pageSize: 20, totalPages: 1 });
  });

  it('should fetch payslips for a pay period', () => {
    service.getPayPeriodPayslips('p1').subscribe(res => {
      expect(res).toHaveLength(1);
      expect(res[0].grossPay).toBe(2500);
    });

    const req = httpTesting.expectOne('/api/payroll/periods/p1/payslips');
    expect(req.request.method).toBe('GET');
    req.flush([{ id: 'ps1', payPeriodId: 'p1', driverId: 'd1', driverName: 'John', grossPay: 2500, regularHours: 80, overtimeHours: 10, holidayHours: 0, netPay: 2000, status: 'Finalised', employeeNo: 'E1', startsOn: '2026-08-01', endsOn: '2026-08-14' }]);
  });
});
