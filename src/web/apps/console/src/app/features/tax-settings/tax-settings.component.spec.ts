import { beforeEach, afterEach, describe, it, expect, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TaxSettingsComponent, nzCalendarDate } from './tax-settings.component';
import { TaxProfile } from '../../core/payroll/tax-profile.models';
import { I18nService } from '../../core/i18n/i18n.service';
import en from '../../../assets/i18n/en-NZ.json';
import zh from '../../../assets/i18n/zh-CN.json';
const pending: TaxProfile = {
  id: 'profile-1',
  driverId: 'driver-1',
  driverName: 'Test Driver',
  employeeNo: 'TEST',
  status: 'Pending',
  effectiveFrom: '2026-09-15',
  declaration: {
    workerType: 'Employee',
    employee: {
      taxCode: 'M SL',
      kiwiSaverEmployee: {
        rate: 0.035,
        contributionsRequired: true,
        temporaryReductionApproved: false,
      },
    },
    contractor: null,
  },
  submittedAt: '2026-09-12T00:00:00Z',
  reviewedAt: null,
  approvedEmployee: null,
  approvedContractor: null,
};
describe('TaxSettingsComponent', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-09-12T00:00:00Z'));
  });
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
    vi.useRealTimers();
  });
  function setup(review = false, lang: 'en-NZ' | 'zh-CN' = 'en-NZ') {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { data: { review } } } },
      ],
    });
    const i18n = TestBed.inject(I18nService);
    i18n.setDictionary('en-NZ', en);
    i18n.setDictionary('zh-CN', zh);
    i18n.currentLang.set(lang);
    const fixture = TestBed.createComponent(TaxSettingsComponent);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    function flush(profiles: TaxProfile[] = [], pendingProfiles: TaxProfile[] = []) {
      const requests = http.match(
        (req) => req.method === 'GET' && req.url.startsWith('/api/payroll/tax-profiles'),
      );
      expect(requests.length).toBe(2);
      requests.forEach((req) => {
        const items = req.request.params.get('status') === 'Pending' ? pendingProfiles : profiles;
        req.flush({ items, totalCount: items.length, page: 1, pageSize: 20 });
      });
      fixture.detectChanges();
    }
    return { fixture, component: fixture.componentInstance, http, flush };
  }
  function employee(c: TaxSettingsComponent) {
    c.effectiveFrom = '2026-09-15';
    c.workerType = 'Employee';
    c.taxCode = 'M SL';
    c.employeeRequired = true;
    c.employeeRate = 3.5;
    c.confirmed = true;
  }
  it('starts with no personal tax choices and sends nothing until confirmed', () => {
    const { component: c, http, flush } = setup();
    flush();
    expect(c.workerType).toBe('');
    expect(c.employeeRequired).toBeNull();
    expect(c.employeeRate).toBeNull();
    expect(c.submission()).toBeNull();
    employee(c);
    c.confirmed = false;
    c.submit();
    http.expectNone((req) => req.method === 'POST');
    c.confirmed = true;
    c.submit();
    const req = http.expectOne('/api/payroll/tax-profiles/mine');
    expect(req.request.body).toMatchObject({
      effectiveFrom: '2026-09-15',
      confirmed: true,
      declaration: {
        workerType: 'Employee',
        employee: {
          taxCode: 'M SL',
          kiwiSaverEmployee: { rate: 0.035, contributionsRequired: true },
        },
        contractor: null,
      },
    });
    expect(req.request.body.declaration.employee.kiwiSaverEmployer).toBeUndefined();
    req.flush(pending);
    flush([pending], [pending]);
  });
  it('blocks duplicate pending requests even when history filtering hides them; supports withdrawal then resubmission', () => {
    const { component: c, http, flush } = setup();
    flush([], [pending]);
    employee(c);
    expect(c.submission()).toBeNull();
    c.withdraw(pending);
    const req = http.expectOne('/api/payroll/tax-profiles/profile-1/withdraw');
    expect(req.request.body).toEqual({});
    req.flush({ ...pending, status: 'Withdrawn' });
    flush([], []);
    c.confirmed = true;
    expect(c.submission()).not.toBeNull();
  });
  it('requires temporary 3% confirmation and explicit contractor withholding/GST verification', () => {
    const { component: c, flush } = setup();
    flush();
    employee(c);
    c.employeeRate = 3;
    expect(c.submission()).toBeNull();
    c.employeeReduction = true;
    expect(c.submission()).not.toBeNull();
    c.workerType = 'Contractor';
    c.withholdingRate = 20;
    c.gstRate = 15;
    expect(c.submission()).toBeNull();
    c.withholdingVerified = true;
    c.gstVerified = true;
    expect(c.submission()?.declaration.employee).toBeNull();
    c.withholdingVerified = false;
    c.exemptionVerified = true;
    expect(c.submission()).toBeNull();
    c.withholdingRate = 0;
    expect(c.submission()).not.toBeNull();
  });
  it('requires explicit employer settings and cannot approve earlier than the requested date', () => {
    const { component: c, http, flush } = setup(true);
    flush([], [pending]);
    c.startReview(pending);
    expect(c.approvalDate).toBe('2026-09-15');
    expect(c.employerRequired).toBeNull();
    expect(c.employerRate).toBeNull();
    expect(c.approval()).toBeNull();
    c.employerRequired = true;
    c.employerRate = 3.5;
    c.esctRate = 30;
    c.holidayMode = 'OrdinaryAccrual';
    c.approvalConfirmed = true;
    expect(c.approval()).toBeNull();
    c.esctVerified = true;
    c.approvalDate = '2026-09-14';
    expect(c.approval()).toBeNull();
    c.approvalDate = '2026-09-15';
    c.approve();
    const req = http.expectOne('/api/payroll/tax-profiles/profile-1/approve');
    expect(req.request.body.kiwiSaverEmployer.rate).toBe(0.035);
    expect(req.request.body.employee).toBeUndefined();
    expect(req.request.body.declaration).toBeUndefined();
    req.flush({ ...pending, status: 'Approved' });
    flush();
  });
  it('rejects a pending request without collecting a free-text reason or editing the declaration', () => {
    const { component: c, http, flush } = setup(true);
    flush([], [pending]);
    c.startReview(pending);
    c.reject();
    const req = http.expectOne('/api/payroll/tax-profiles/profile-1/reject');
    expect(req.request.body).toEqual({});
    req.flush({ ...pending, status: 'Rejected' });
    flush();
    c.startReview({ ...pending, status: 'Approved' });
    expect(c.selected()).toBeNull();
  });
  it('uses Auckland calendar boundaries and rejects invalid or past effective dates', () => {
    const { component: c, flush } = setup();
    flush();
    expect(nzCalendarDate(new Date('2026-09-11T13:00:00Z'))).toBe('2026-09-12');
    expect(c.validDate('2026-09-11')).toBe(false);
    expect(c.validDate('2026-09-31')).toBe(false);
    expect(c.validDate('2027-04-01')).toBe(false);
  });
  it.each(['en-NZ', 'zh-CN'] as const)('renders business statuses and NZ dates in %s', (lang) => {
    const { fixture, flush } = setup(false, lang);
    flush([{ ...pending, status: 'Approved' }]);
    expect(fixture.nativeElement.textContent).toContain(lang === 'en-NZ' ? 'Approved' : '已批准');
    expect(fixture.nativeElement.textContent).toContain('15/09/2026');
    expect(fixture.nativeElement.textContent).not.toContain('TAX_PROFILE.');
  });
  it('cancels personal-data requests on navigation', () => {
    const { fixture, http } = setup();
    const pendingRequests = http.match((req) => req.method === 'GET');
    fixture.destroy();
    expect(pendingRequests.every((req) => req.cancelled)).toBe(true);
  });
});
