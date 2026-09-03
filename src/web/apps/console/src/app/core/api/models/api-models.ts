// Domain enums - Single Source of Truth matching C# Nimpression.Domain.Enums
export type VehicleStatus = 'Active' | 'Maintenance' | 'Inactive' | 'Decommissioned';
export type JobTaskStatus =
  | 'Draft'
  | 'Assigned'
  | 'Acknowledged'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled';
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Urgent';
export type ShiftStatus = 'Active' | 'Completed' | 'AutoClosed' | 'Cancelled';
export type FineStatus = 'Submitted' | 'UnderReview' | 'Accepted' | 'Disputed' | 'Waived';
export type PayPeriodStatus = 'Open' | 'Calculating' | 'Finalised' | 'Paid';
export type PayBasis = 'Hourly' | 'Trip';
export type DriverStatus = 'Active' | 'Inactive' | 'Suspended' | 'OnLeave' | 'Terminated';
export type UserRole = 'Admin' | 'Dispatcher' | 'Driver';
export type UserStatus = 'Active' | 'Inactive' | 'Suspended';
export type IncidentSeverity = 'Minor' | 'Moderate' | 'Major' | 'Critical';
export type DataSubjectRequestKind = 'Export' | 'Deletion' | 'Rectification';
export type NewsAudience = 'All' | 'Drivers' | 'Dispatchers';
export type PartnerKind = 'Insurer' | 'Maintenance' | 'Inspection';

export interface VehicleDto {
  id: string;
  rego: string;
  make: string;
  model: string;
  year: number;
  odometerKm: number;
  serviceIntervalKm: number;
  lastServiceOdometerKm?: number | null;
  wofExpiry?: string | null;
  cofExpiry?: string | null;
  insuranceExpiry?: string | null;
  status: VehicleStatus;
}

export interface OdometerReadingDto {
  id: string;
  readingKm: number;
  recordedAt: string;
  source: string;
  driverId?: string | null;
  driverName?: string | null;
  photoUrl?: string | null;
}

export interface JobTaskDto {
  id: string;
  ref?: string | null;
  title: string;
  status: JobTaskStatus;
  priority: TaskPriority;
  scheduledFor: string;
  startedAt?: string | null;
  completedAt?: string | null;
  cancelledAt?: string | null;
  driverId?: string | null;
  driverName?: string | null;
  vehicleId?: string | null;
  vehicleRego?: string | null;
  areaId?: string | null;
  areaName?: string | null;
  plannedDistanceKm?: number | null;
  actualDistanceKm?: number | null;
  createdAt: string;
  acknowledgedAt?: string | null;
}

export interface TimesheetDto {
  id: string;
  driverId: string;
  driverName?: string | null;
  vehicleId?: string | null;
  clockInAt: string;
  clockOutAt?: string | null;
  durationMinutes?: number | null;
  breakMinutes?: number | null;
  netWorkMinutes?: number | null;
  status: ShiftStatus;
  createdAt: string;
}

export interface FineDto {
  id: string;
  driverId?: string | null;
  driverName?: string | null;
  vehicleId: string;
  vehicleRego?: string | null;
  issuedOn: string;
  authority: string;
  reference: string;
  amount: number;
  currency: string;
  reason: string;
  status: FineStatus;
  ticketPhotoUrl?: string | null;
}

export interface PayPeriodDto {
  id: string;
  startsOn: string;
  endsOn: string;
  status: PayPeriodStatus;
  totalGrossPay?: number;
}

export interface PayslipDto {
  id: string;
  payPeriodId: string;
  driverId: string;
  driverName: string;
  employeeNo: string;
  startsOn: string;
  endsOn: string;
  regularHours: number;
  overtimeHours: number;
  holidayHours: number;
  grossPay: number;
  netPay: number;
  status: string;
}

export interface DriverDto {
  id: string;
  displayName: string;
  employeeNo: string;
  status: DriverStatus;
}

export interface PaginatedList<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}
