import { describe, it, expect } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { SettlementBreakdownComponent } from './settlement-breakdown.component';
import { I18nService } from '../../../core/i18n/i18n.service';
import { PayslipSettlement } from '../../../core/payroll/settlement.models';
import en from '../../../../assets/i18n/en-NZ.json';
import zh from '../../../../assets/i18n/zh-CN.json';
const snapshot: PayslipSettlement = {
  request: {
    payDate: '2026-09-15',
    frequency: 'Weekly',
    workerType: 'Employee',
    grossEarnings: 1000,
    employee: {
      taxCode: 'M',
      kiwiSaverEmployee: {
        rate: 0.035,
        contributionsRequired: true,
        temporaryReductionApproved: false,
      },
      kiwiSaverEmployer: {
        rate: 0.035,
        contributionsRequired: true,
        temporaryReductionApproved: false,
        esctRate: 0.175,
        esctRateVerified: true,
      },
      holidayPay: {
        mode: 'OrdinaryAccrual',
        eligibilityConfirmed: false,
        writtenAgreementConfirmed: false,
        eligibility: null,
      },
    },
    contractor: null,
  },
  calculation: {
    taxYear: '2026/27',
    baseGross: 1000,
    holidayPay: 0,
    taxableGross: 1000,
    incomeTax: 100,
    accEarnersLevy: 17,
    paye: 117,
    studentLoan: 20,
    employeeKiwiSaver: 35,
    employerKiwiSaverGross: 35,
    esct: 6.13,
    employerKiwiSaverNet: 28.87,
    contractorWithholding: 0,
    gst: 0,
    netPay: 828,
    employerCost: 1035,
  },
  rulesVersion: 'test',
  calculatedAt: '2026-09-15T00:00:00Z',
};
describe('SettlementBreakdownComponent', () => {
  it.each(['en-NZ', 'zh-CN'] as const)(
    'explains employee and employer deductions separately in %s',
    (lang) => {
      const i18n = TestBed.inject(I18nService);
      i18n.setDictionary('en-NZ', en);
      i18n.setDictionary('zh-CN', zh);
      i18n.currentLang.set(lang);
      const fixture = TestBed.createComponent(SettlementBreakdownComponent);
      fixture.componentRef.setInput('settlement', snapshot);
      fixture.detectChanges();
      const text = fixture.nativeElement.textContent;
      expect(text).toContain('15/09/2026');
      expect(text).toContain('828.00');
      expect(text).toContain('117.00');
      expect(text).toContain(lang === 'en-NZ' ? 'not deducted again' : '不再重复扣除');
      expect(text).toContain(lang === 'en-NZ' ? 'separate from deductions' : '不从您的工资中扣除');
      expect(text).not.toContain('SETTLEMENT.');
      expect(text).toContain(lang === 'en-NZ' ? 'Scheduled pay date' : '计划发薪日期');
      expect(text).toContain(
        lang === 'en-NZ'
          ? 'leave balances and leave taken are not calculated here'
          : '此处不计算年假余额或已休假期',
      );
    },
  );
  it('shows the cash holiday pay row for the verified 8% mode', () => {
    const i18n = TestBed.inject(I18nService);
    i18n.setDictionary('en-NZ', en);
    const fixture = TestBed.createComponent(SettlementBreakdownComponent);
    fixture.componentRef.setInput('settlement', {
      ...snapshot,
      request: {
        ...snapshot.request,
        employee: { ...snapshot.request.employee, holidayPay: { mode: 'PayAsYouGoEightPercent' } },
      },
      calculation: { ...snapshot.calculation, holidayPay: 80 },
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('80.00');
    expect(fixture.nativeElement.textContent).toContain('Pay 8% holiday pay');
    expect(fixture.nativeElement.textContent).not.toContain(
      'leave balances and leave taken are not calculated here',
    );
  });

  it('shows contractor GST separately without employee deductions', () => {
    const i18n = TestBed.inject(I18nService);
    i18n.setDictionary('en-NZ', en);
    const fixture = TestBed.createComponent(SettlementBreakdownComponent);
    fixture.componentRef.setInput('settlement', {
      ...snapshot,
      request: { ...snapshot.request, workerType: 'Contractor' },
      calculation: { ...snapshot.calculation, contractorWithholding: 200, gst: 150, netPay: 950 },
    });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('GST added to payment');
    expect(text).toContain('150.00');
    expect(text).toContain('950.00');
    expect(text).not.toContain('Employee KiwiSaver deduction');
  });
});
