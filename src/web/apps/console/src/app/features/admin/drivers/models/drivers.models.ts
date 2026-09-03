export type DriverStatus = 'Active' | 'Inactive' | 'Suspended' | 'OnLeave' | 'Terminated';

export interface AreaAssignmentDto {
  id: string;
  areaId: string;
  areaName: string;
  areaCode: string;
  driverId: string;
  effectiveFrom: string;
  effectiveTo?: string | null;
  isActive: boolean;
}

export interface DriverSummaryDto {
  id: string;
  userId: string;
  employeeNo: string;
  displayName: string;
  email: string;
  licenceClass: string;
  licenceExpiry: string;
  isLicenceExpiringSoon: boolean;
  isLicenceExpired: boolean;
  daysUntilLicenceExpiry: number;
  status: DriverStatus;
  hiredOn: string;
  hourlyRate: number;
  perTripRate: number;
  perKmRate: number;
  assignedAreaNames: string[];
  activeAreaIds: string[];
  avatarUrl?: string | null;
}

export interface DriverDetailDto {
  id: string;
  userId: string;
  employeeNo: string;
  displayName: string;
  email: string;
  licenceClass: string;
  licenceExpiry: string;
  isLicenceExpiringSoon: boolean;
  isLicenceExpired: boolean;
  daysUntilLicenceExpiry: number;
  status: DriverStatus;
  hiredOn: string;
  hourlyRateAmount: number;
  hourlyRateCurrency: string;
  perTripRateAmount: number;
  perTripRateCurrency: string;
  perKmRateAmount: number;
  perKmRateCurrency: string;
  phone: string;
  address: string;
  emergencyContact: string;
  locale: string;
  avatarKey?: string | null;
  avatarUrl?: string | null;
  areaAssignments: AreaAssignmentDto[];
}

export interface DriverLicenceAlertDto {
  driverId: string;
  userId: string;
  employeeNo: string;
  displayName: string;
  licenceClass: string;
  licenceExpiry: string;
  daysUntilExpiry: number;
  isExpired: boolean;
  status: DriverStatus;
}

export interface DriverFilter {
  searchTerm?: string;
  name?: string;
  employeeNo?: string;
  status?: DriverStatus;
  areaId?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateDriverRequest {
  displayName: string;
  email: string;
  password?: string;
  employeeNo: string;
  licenceClass: string;
  licenceExpiry: string;
  hourlyRateAmount: number;
  hourlyRateCurrency?: string;
  perTripRateAmount: number;
  perTripRateCurrency?: string;
  perKmRateAmount: number;
  perKmRateCurrency?: string;
  phone: string;
  address: string;
  emergencyContact: string;
  hiredOn: string;
  areaIds?: string[];
}

export interface UpdateDriverRequest {
  displayName: string;
  licenceClass: string;
  licenceExpiry: string;
  hourlyRateAmount: number;
  hourlyRateCurrency?: string;
  perTripRateAmount: number;
  perTripRateCurrency?: string;
  perKmRateAmount: number;
  perKmRateCurrency?: string;
  phone: string;
  address: string;
  emergencyContact: string;
  status: DriverStatus;
}

export interface DeactivateDriverRequest {
  reason?: string;
}

export interface UploadAvatarResultDto {
  avatarKey: string;
  avatarUrl: string;
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}
