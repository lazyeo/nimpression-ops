export type { FineStatus } from '../../../../core/api/models/api-models';
import type { FineStatus } from '../../../../core/api/models/api-models';

export interface FineDto {
  id: string;
  driverId: string;
  driverName: string;
  employeeNo: string;
  vehicleId: string;
  vehicleRego: string;
  issuedOn: string;
  authority: string;
  reference: string;
  amount: number;
  currency: string;
  reason: string;
  status: FineStatus;
  ticketPhotoKey?: string | null;
  reviewedAt?: string | null;
  reviewNote?: string | null;
}

export interface FineDetailDto {
  id: string;
  driverId: string;
  driverName: string;
  employeeNo: string;
  vehicleId: string;
  vehicleRego: string;
  issuedOn: string;
  authority: string;
  reference: string;
  amount: number;
  currency: string;
  reason: string;
  status: FineStatus;
  ticketPhotoKey?: string | null;
  ticketPhotoUrl?: string | null;
  reviewedByUserId?: string | null;
  reviewerName?: string | null;
  reviewedAt?: string | null;
  reviewNote?: string | null;
}

export interface SubmitFineRequest {
  driverId?: string | null;
  vehicleId: string;
  issuedOn: string;
  authority: string;
  reference: string;
  amount: number;
  currency?: string | null;
  reason: string;
  ticketPhotoKey?: string | null;
}

export interface AcceptFineRequest {
  reviewNote?: string | null;
}

export interface DisputeFineRequest {
  reviewNote: string;
}

export interface WaiveFineRequest {
  reviewNote: string;
}

export interface FineFilterParams {
  driverId?: string;
  vehicleId?: string;
  status?: FineStatus;
  fromDate?: string;
  toDate?: string;
  searchTerm?: string;
  page?: number;
  pageSize?: number;
}
