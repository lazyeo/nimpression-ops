export type { ShiftStatus } from '../../../../core/api/models/api-models';
import type { ShiftStatus } from '../../../../core/api/models/api-models';

export interface ShiftEntryDto {
  id: string;
  driverId: string;
  driverName?: string | null;
  clockInAt: string;
  clockInLat?: number | null;
  clockInLng?: number | null;
  locationUnavailable: boolean;
  clockOutAt?: string | null;
  clockOutLat?: number | null;
  clockOutLng?: number | null;
  vehicleId?: string | null;
  breakMinutes: number;
  note?: string | null;
  status: ShiftStatus;
  attributedDate?: string | null;
  rawDurationHours?: number | null;
  payableHours?: number | null;
  adminCorrectionReason?: string | null;
  correctedByUserId?: string | null;
  correctedAt?: string | null;
}

export interface TimesheetDailySummaryDto {
  date: string;
  shiftCount: number;
  payableHours: number;
  ordinaryHours: number;
  overtimeHours: number;
  breakMinutes: number;
}

export interface TimesheetSummaryDto {
  driverId?: string | null;
  driverName?: string | null;
  fromDate: string;
  toDate: string;
  totalShifts: number;
  totalPayableHours: number;
  totalOrdinaryHours: number;
  totalOvertimeHours: number;
  totalBreakMinutes: number;
  dailySummaries: TimesheetDailySummaryDto[];
}

export interface AdminCorrectShiftRequest {
  newClockInAt: string;
  newClockOutAt?: string | null;
  newBreakMinutes?: number | null;
  reason: string;
}

export interface TimesheetFilterParams {
  driverId?: string;
  fromDate?: string;
  toDate?: string;
  status?: ShiftStatus;
  page?: number;
  pageSize?: number;
}

export interface DriverOption {
  id: string;
  displayName: string;
  employeeNo: string;
}
