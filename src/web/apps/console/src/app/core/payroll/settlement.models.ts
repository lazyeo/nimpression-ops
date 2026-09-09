export type PayFrequency = 'Weekly' | 'Fortnightly' | 'FourWeekly' | 'Monthly';
export type WorkerType = 'Employee' | 'Contractor';
export type HolidayMode = 'OrdinaryAccrual' | 'PayAsYouGoEightPercent';
export type HolidayEligibility =
  'GenuineFixedTermUnderTwelveMonths' | 'IrregularWorkPrecludesAnnualHolidays';
export interface SettlementRequest {
  payDate: string;
  frequency: PayFrequency;
  grossEarnings: number;
  workerType: WorkerType;
  employee: {
    taxCode: string;
    kiwiSaverEmployee: {
      rate: number;
      contributionsRequired: boolean;
      temporaryReductionApproved: boolean;
    };
    kiwiSaverEmployer: {
      rate: number;
      contributionsRequired: boolean;
      temporaryReductionApproved: boolean;
      esctRate: number | null;
      esctRateVerified: boolean;
    };
    holidayPay: {
      mode: HolidayMode;
      eligibilityConfirmed: boolean;
      writtenAgreementConfirmed: boolean;
      eligibility: HolidayEligibility | null;
    };
  } | null;
  contractor: {
    withholdingRate: number;
    withholdingVerified: boolean;
    exemptionVerified: boolean;
    gstRate: number;
    gstTreatmentVerified: boolean;
  } | null;
}
export interface SettlementCalculation {
  taxYear: string;
  baseGross: number;
  holidayPay: number;
  taxableGross: number;
  incomeTax: number;
  accEarnersLevy: number;
  paye: number;
  studentLoan: number;
  employeeKiwiSaver: number;
  employerKiwiSaverGross: number;
  esct: number;
  employerKiwiSaverNet: number;
  contractorWithholding: number;
  gst: number;
  netPay: number;
  employerCost: number;
}
export interface PayslipSettlement {
  request: SettlementRequest;
  calculation: SettlementCalculation;
  rulesVersion: string;
  calculatedAt: string;
}
