export type { VehicleStatus } from '../../../../core/api/models/api-models';
import type { VehicleStatus } from '../../../../core/api/models/api-models';

export interface VehicleSummaryDto {
  id: string;
  rego: string;
  make: string;
  model: string;
  year: number;
  odometerKm: number;
  serviceIntervalKm: number;
  lastServiceOdometerKm: number;
  distanceSinceLastServiceKm: number;
  isServiceDue: boolean;
  wofExpiry?: string | null;
  cofExpiry?: string | null;
  insuranceExpiry?: string | null;
  status: VehicleStatus;
  currentDriverId?: string | null;
  currentDriverName?: string | null;
}

export interface VehicleAssignmentDto {
  id: string;
  vehicleId: string;
  vehicleRego?: string | null;
  driverId: string;
  driverName?: string | null;
  driverEmployeeNo?: string | null;
  assignedAt: string;
  releasedAt?: string | null;
  assignedByUserId: string;
  assignedByUserName?: string | null;
  isActive: boolean;
}

export interface OdometerReadingDto {
  id: string;
  vehicleId: string;
  driverId: string;
  driverName?: string | null;
  readingKm: number;
  photoKey?: string | null;
  recordedAt: string;
  source: string;
}

export interface VehicleDetailDto {
  id: string;
  rego: string;
  make: string;
  model: string;
  year: number;
  vinEnc: string;
  odometerKm: number;
  serviceIntervalKm: number;
  lastServiceOdometerKm: number;
  distanceSinceLastServiceKm: number;
  isServiceDue: boolean;
  wofExpiry?: string | null;
  cofExpiry?: string | null;
  insuranceExpiry?: string | null;
  status: VehicleStatus;
  activeAssignment?: VehicleAssignmentDto | null;
  latestOdometerReading?: OdometerReadingDto | null;
}

export interface VehicleFilter {
  search?: string;
  status?: VehicleStatus;
  serviceDueOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export interface CreateVehicleRequest {
  rego: string;
  make: string;
  model: string;
  year: number;
  vinEnc: string;
  odometerKm: number;
  serviceIntervalKm: number;
  lastServiceOdometerKm?: number;
  wofExpiry?: string;
  cofExpiry?: string;
  insuranceExpiry?: string;
  status?: VehicleStatus;
}

export interface UpdateVehicleRequest {
  wofExpiry?: string;
  cofExpiry?: string;
  insuranceExpiry?: string;
  status: VehicleStatus;
}

export interface AssignVehicleRequest {
  driverId: string;
  assignedAt?: string;
}

export interface RecordOdometerReadingRequest {
  driverId: string;
  readingKm: number;
  photoKey?: string;
  recordedAt?: string;
  source?: string;
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
