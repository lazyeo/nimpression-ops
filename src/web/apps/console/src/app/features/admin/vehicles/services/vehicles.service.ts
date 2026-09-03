import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AssignVehicleRequest,
  CreateVehicleRequest,
  OdometerReadingDto,
  PaginatedResult,
  RecordOdometerReadingRequest,
  UpdateVehicleRequest,
  VehicleAssignmentDto,
  VehicleDetailDto,
  VehicleFilter,
  VehicleStatus,
  VehicleSummaryDto,
} from '../models/vehicles.models';

export interface DriverLookupOption {
  id: string;
  displayName: string;
  employeeNo: string;
  status: string;
}

@Injectable({
  providedIn: 'root',
})
export class VehiclesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/vehicles';

  getVehicles(filter: VehicleFilter = {}): Observable<PaginatedResult<VehicleSummaryDto>> {
    let params = new HttpParams();
    if (filter.search) params = params.set('search', filter.search);
    if (filter.status) params = params.set('status', filter.status);
    if (filter.serviceDueOnly !== undefined) {
      params = params.set('serviceDueOnly', filter.serviceDueOnly.toString());
    }
    if (filter.page) params = params.set('page', filter.page.toString());
    if (filter.pageSize) params = params.set('pageSize', filter.pageSize.toString());

    return this.http.get<PaginatedResult<VehicleSummaryDto>>(`${this.baseUrl}`, { params });
  }

  getVehicleById(id: string): Observable<VehicleDetailDto> {
    return this.http.get<VehicleDetailDto>(`${this.baseUrl}/${id}`);
  }

  createVehicle(request: CreateVehicleRequest): Observable<string> {
    return this.http.post<string>(`${this.baseUrl}`, request);
  }

  updateVehicle(id: string, request: UpdateVehicleRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, request);
  }

  updateVehicleStatus(id: string, status: VehicleStatus): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${id}/status`, { status });
  }

  recordService(id: string, serviceOdometerKm: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/service`, { serviceOdometerKm });
  }

  assignVehicle(id: string, request: AssignVehicleRequest): Observable<string> {
    return this.http.post<string>(`${this.baseUrl}/${id}/assignments`, request);
  }

  releaseAssignment(assignmentId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/assignments/${assignmentId}/release`, {});
  }

  getActiveAssignment(id: string): Observable<VehicleAssignmentDto | null> {
    return this.http.get<VehicleAssignmentDto | null>(`${this.baseUrl}/${id}/assignments/active`);
  }

  getVehicleAssignments(id: string): Observable<VehicleAssignmentDto[]> {
    return this.http.get<VehicleAssignmentDto[]>(`${this.baseUrl}/${id}/assignments`);
  }

  recordOdometerReading(id: string, request: RecordOdometerReadingRequest): Observable<string> {
    return this.http.post<string>(`${this.baseUrl}/${id}/odometer`, request);
  }

  getOdometerReadings(id: string, limit = 50): Observable<OdometerReadingDto[]> {
    const params = new HttpParams().set('limit', limit.toString());
    return this.http.get<OdometerReadingDto[]>(`${this.baseUrl}/${id}/odometer`, { params });
  }

  getDrivers(): Observable<PaginatedResult<DriverLookupOption>> {
    const params = new HttpParams().set('pageSize', '100');
    return this.http.get<PaginatedResult<DriverLookupOption>>('/api/drivers', { params });
  }
}
