export type { IncidentSeverity } from '../../../../core/api/models/api-models';
import type { IncidentSeverity } from '../../../../core/api/models/api-models';

export interface IncidentReportDto {
  id: string;
  driverId: string;
  driverName: string;
  employeeNo: string;
  vehicleId: string;
  vehicleRego: string;
  occurredAt: string;
  location: string;
  severity: IncidentSeverity;
  description: string;
  thirdPartyInfo?: string | null;
  status: string;
  insurerNotifiedAt?: string | null;
  photoKeys: string[];
  notifiedInsurer: boolean;
}

export interface IncidentReportDetailDto {
  id: string;
  driverId: string;
  driverName: string;
  employeeNo: string;
  vehicleId: string;
  vehicleRego: string;
  occurredAt: string;
  location: string;
  severity: IncidentSeverity;
  description: string;
  thirdPartyInfo?: string | null;
  status: string;
  insurerNotifiedAt?: string | null;
  photoKeys: string[];
  photoUrls: string[];
  notifiedInsurer: boolean;
}

export interface ReportIncidentRequest {
  driverId?: string | null;
  vehicleId: string;
  occurredAt: string;
  location: string;
  severity: IncidentSeverity;
  description: string;
  photoKeys?: string[] | null;
  thirdPartyInfo?: string | null;
}

export interface IncidentFilterParams {
  driverId?: string;
  vehicleId?: string;
  severity?: IncidentSeverity;
  fromDate?: string;
  toDate?: string;
  searchTerm?: string;
  page?: number;
  pageSize?: number;
}
