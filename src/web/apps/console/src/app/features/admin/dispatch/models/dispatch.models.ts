export type {
  JobTaskStatus,
  TaskPriority,
} from '../../../../core/api/models/api-models';
import type {
  JobTaskStatus,
  TaskPriority,
} from '../../../../core/api/models/api-models';

export interface JobTaskDetailDto {
  id: string;
  ref: string;
  title: string;
  description?: string | null;
  areaId: string;
  areaName: string;
  areaCode: string;
  driverId?: string | null;
  driverName?: string | null;
  vehicleId?: string | null;
  vehicleRego?: string | null;
  scheduledFor: string;
  priority: TaskPriority;
  status: JobTaskStatus;
  acknowledgedAt?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  cancelledAt?: string | null;
  cancellationReason?: string | null;
  createdByUserId: string;
  createdByUserName?: string | null;
  plannedDistanceKm?: number | null;
  actualDistanceKm?: number | null;
  startOdometerKm?: number | null;
  endOdometerKm?: number | null;
  effectiveDistanceKm?: number | null;
}

export interface JobTaskAlertDto {
  taskId: string;
  ref: string;
  title: string;
  driverId: string;
  driverName?: string | null;
  vehicleId?: string | null;
  vehicleRego?: string | null;
  areaId: string;
  areaName: string;
  scheduledFor: string;
  minutesUnacknowledged: number;
}

export interface AreaEligibilityCheckDto {
  isAssignedToArea: boolean;
  requiresWarning: boolean;
  warningMessage?: string | null;
}

export interface JobTaskFilter {
  driverId?: string;
  vehicleId?: string;
  areaId?: string;
  status?: JobTaskStatus;
  from?: string;
  to?: string;
  searchTerm?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateJobTaskRequest {
  ref?: string;
  title: string;
  areaId: string;
  scheduledFor: string;
  priority?: TaskPriority;
  description?: string;
  plannedDistanceKm?: number;
  driverId?: string;
  vehicleId?: string;
  overrideAreaWarning?: boolean;
  clientRequestId?: string;
}

export interface AssignJobTaskRequest {
  driverId: string;
  vehicleId: string;
  scheduledFor?: string;
  overrideAreaWarning?: boolean;
}

export interface AcknowledgeJobTaskRequest {
  acknowledgedAt?: string;
  clientRequestId?: string;
}

export interface StartJobTaskRequest {
  startedAt?: string;
  startOdometerKm?: number;
  clientRequestId?: string;
}

export interface CompleteJobTaskRequest {
  completedAt?: string;
  actualDistanceKm?: number;
  endOdometerKm?: number;
  clientRequestId?: string;
}

export interface CancelJobTaskRequest {
  reason: string;
  cancelledAt?: string;
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
