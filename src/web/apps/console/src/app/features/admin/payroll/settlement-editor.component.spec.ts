import { describe, it, expect, afterEach, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { SettlementEditorComponent } from './settlement-editor.component';
import { I18nService } from '../../../core/i18n/i18n.service';
import en from '../../../../assets/i18n/en-NZ.json';
import zh from '../../../../assets/i18n/zh-CN.json';

describe('SettlementEditorComponent', () => {
  function setup(lang: 'en' | 'zh' = 'en') {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const i18n = TestBed.inject(I18nService);
    i18n.setDictionary('en-NZ', en);
    i18n.setDictionary('zh-CN', zh);
    i18n.currentLang.set(lang === 'en' ? 'en-NZ' : 'zh-CN');
    const fixture = TestBed.createComponent(SettlementEditorComponent);
    fixture.componentRef.setInput('payslip', { id: 'slip-1', currency: 'NZD', finalisedAt: null });
    return {
      fixture,
      component: fixture.componentInstance,
      http: TestBed.inject(HttpTestingController),
    };
  }
  function base(c: SettlementEditorComponent) {
    c.payDate = '2026-09-15';
    c.frequency = 'Fortnightly';
  }
  function employee(c: SettlementEditorComponent) {
    base(c);
    c.workerType = 'Employee';
    c.taxCode = 'M SL';
    c.employeeRequired = true;
    c.employeeRate = 3.5;
    c.employerRequired = true;
    c.employerRate = 3.5;
    c.esctRate = 17.5;
    c.esctVerified = true;
    c.holidayMode = 'OrdinaryAccrual';
  }
  afterEach(() => TestBed.inject(HttpTestingController).verify());
  it('leaves every personal tax choice unset and blocks submission', () => {
    const { component, http } = setup();
    expect(component.buildRequest()).toBeNull();
    component.submit();
    http.expectNone('/api/payroll/payslips/slip-1/settlement');
    expect(component.employeeRequired).toBeNull();
    expect(component.employerRequired).toBeNull();
    expect(component.workerType).toBe('');
  });
  it('posts only explicit employee inputs with server-derived gross and saves response', () => {
    const { component, http } = setup();
    employee(component);
    const saved = vi.fn();
    component.saved.subscribe(saved);
    component.submit();
    const req = http.expectOne('/api/payroll/payslips/slip-1/settlement');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toMatchObject({
      grossEarnings: 0,
      workerType: 'Employee',
      payDate: '2026-09-15',
      frequency: 'Fortnightly',
      contractor: null,
      employee: {
        taxCode: 'M SL',
        kiwiSaverEmployee: { rate: 0.035 },
        kiwiSaverEmployer: { esctRate: 0.175 },
      },
    });
    req.flush({ id: 'slip-1', settlementStatus: 'Calculated' });
    expect(saved).toHaveBeenCalledWith({ id: 'slip-1', settlementStatus: 'Calculated' });
  });
  it('blocks unverified ESCT, temporary reduction and holiday eligibility', () => {
    const { component: c } = setup();
    employee(c);
    c.esctVerified = false;
    expect(c.buildRequest()).toBeNull();
    c.esctVerified = true;
    c.employeeRate = 3;
    expect(c.buildRequest()).toBeNull();
    c.employeeReduction = true;
    c.holidayMode = 'PayAsYouGoEightPercent';
    expect(c.buildRequest()).toBeNull();
    c.holidayEligibility = 'GenuineFixedTermUnderTwelveMonths';
    c.eligibilityConfirmed = true;
    expect(c.buildRequest()).toBeNull();
    c.writtenAgreement = true;
    expect(c.buildRequest()?.employee?.holidayPay.mode).toBe('PayAsYouGoEightPercent');
  });
  it('requires verified contractor tax and GST, never sends stale employee profile', () => {
    const { component: c } = setup();
    employee(c);
    c.workerType = 'Contractor';
    c.withholdingRate = 20;
    c.gstRate = 15;
    expect(c.buildRequest()).toBeNull();
    c.withholdingVerified = true;
    expect(c.buildRequest()).toBeNull();
    c.gstVerified = true;
    expect(c.buildRequest()).toMatchObject({
      employee: null,
      contractor: { withholdingRate: 0.2, gstRate: 0.15 },
    });
    c.withholdingVerified = false;
    c.exemptionVerified = true;
    expect(c.buildRequest()).toBeNull();
    c.withholdingRate = 0;
    expect(c.buildRequest()?.contractor?.withholdingRate).toBe(0);
  });
  it('does not post for finalised payslips', () => {
    const { component: c, fixture, http } = setup();
    employee(c);
    fixture.componentRef.setInput('payslip', {
      id: 'slip-1',
      currency: 'NZD',
      finalisedAt: '2026-09-16T00:00:00Z',
    });
    c.submit();
    http.expectNone('/api/payroll/payslips/slip-1/settlement');
  });
  it('prefills an existing draft settlement and posts corrected tax settings', () => {
    const { component: c, fixture, http } = setup();
    employee(c);
    const stored = c.buildRequest();
    fixture.componentRef.setInput('payslip', {
      id: 'slip-1',
      currency: 'NZD',
      settlement: { request: stored },
    });
    fixture.detectChanges();
    expect(c.employeeRate).toBe(3.5);
    expect(c.taxCode).toBe('M SL');
    expect(c.payDate).toBe('2026-09-15');
    expect(c.buildRequest()).toEqual(stored);
    c.taxCode = 'M';
    c.submit();
    const req = http.expectOne('/api/payroll/payslips/slip-1/settlement');
    expect(req.request.body.employee.taxCode).toBe('M');
    req.flush({ id: 'slip-1', settlementStatus: 'Calculated' });
  });

  it.each(['en', 'zh'] as const)('renders localized business instructions in %s', (lang) => {
    const { fixture } = setup(lang);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain(
      lang === 'en' ? 'Working arrangement' : '用工类型',
    );
    expect(fixture.nativeElement.textContent).not.toContain('SETTLEMENT.');
  });
});
