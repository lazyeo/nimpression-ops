export type {
  PayPeriodStatus,
  PayBasis,
  FineStatus,
} from '../../../../core/api/models/api-models';
import type {
  PayPeriodStatus,
  PayBasis,
  FineStatus,
} from '../../../../core/api/models/api-models';

export interface PayPeriodDto {
  id: string;
  startsOn: string;
  endsOn: string;
  status: PayPeriodStatus;
  finalisedAt?: string | null;
  paidAt?: string | null;
  payslipCount: number;
}

export interface PayslipLineDto {
  id: string;
  basis: PayBasis;
  kind: string;
  description: string;
  rate: number;
  currency: string;
  amount: number;
  hours?: number | null;
  distance?: number | null;
  qty?: number | null;
}

export interface PayslipShiftDetailDto {
  shiftId: string;
  clockInAt: string;
  clockOutAt?: string | null;
  breakMinutes: number;
  attributedDate: string;
  payableHours: number;
}

export interface PayslipTripDetailDto {
  jobTaskId: string;
  ref: string;
  title: string;
  completedAt?: string | null;
  effectiveDistanceKm?: number | null;
}

export interface PayslipFineDto {
  fineId: string;
  reference: string;
  issuedOn: string;
  authority: string;
  amount: number;
  currency: string;
  status: FineStatus;
  reason: string;
}

export interface PayslipDto {
  id: string;
  payPeriodId: string;
  periodStartsOn: string;
  periodEndsOn: string;
  driverId: string;
  driverName?: string | null;
  employeeNo?: string | null;
  ordinaryHours: number;
  overtimeHours: number;
  holidayHours: number;
  hourlyRateSnapshot: number;
  hoursBasedGross: number;
  completedTripCount: number;
  totalDistanceKm: number;
  perTripRateSnapshot: number;
  perKmRateSnapshot: number;
  tripBasedGross: number;
  basisUsed: PayBasis;
  grossPay: number;
  currency: string;
  minimumWageTopUp: boolean;
  calculatedAt: string;
  finalisedAt?: string | null;
  lines: PayslipLineDto[];
  shiftDetails: PayslipShiftDetailDto[];
  tripDetails: PayslipTripDetailDto[];
  fines: PayslipFineDto[];
  finesLegalNotice: string;
}

export interface CreatePayPeriodRequest {
  startsOn: string;
  endsOn?: string | null;
}

export interface CalculatePayrollRequest {
  driverId?: string | null;
  publicHolidays?: string[] | null;
  minimumHourlyWage?: number | null;
}

export interface VoidPayPeriodRequest {
  reason: string;
}

export interface PayPeriodFilterParams {
  fromDate?: string;
  toDate?: string;
  status?: PayPeriodStatus;
  page?: number;
  pageSize?: number;
}
