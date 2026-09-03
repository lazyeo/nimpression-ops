import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AreaEligibilityCheckDto,
  AssignJobTaskRequest,
  AcknowledgeJobTaskRequest,
  CancelJobTaskRequest,
  CompleteJobTaskRequest,
  CreateJobTaskRequest,
  JobTaskAlertDto,
  JobTaskDetailDto,
  JobTaskFilter,
  PaginatedResult,
  StartJobTaskRequest,
} from '../models/dispatch.models';

export interface DriverOption {
  id: string;
  displayName: string;
  employeeNo: string;
  status: string;
  isLicenceExpired: boolean;
}

export interface VehicleOption {
  id: string;
  rego: string;
  make: string;
  model: string;
  status: string;
}

export interface AreaOption {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class DispatchService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/dispatch';

  getTasks(filter: JobTaskFilter = {}): Observable<PaginatedResult<JobTaskDetailDto>> {
    let params = new HttpParams();
    if (filter.driverId) params = params.set('driverId', filter.driverId);
    if (filter.vehicleId) params = params.set('vehicleId', filter.vehicleId);
    if (filter.areaId) params = params.set('areaId', filter.areaId);
    if (filter.status) params = params.set('status', filter.status);
    if (filter.from) params = params.set('from', filter.from);
    if (filter.to) params = params.set('to', filter.to);
    if (filter.searchTerm) params = params.set('searchTerm', filter.searchTerm);
    if (filter.page) params = params.set('page', filter.page.toString());
    if (filter.pageSize) params = params.set('pageSize', filter.pageSize.toString());

    return this.http.get<PaginatedResult<JobTaskDetailDto>>(`${this.baseUrl}/tasks`, { params });
  }

  getUnacknowledgedAlerts(thresholdMinutes = 30): Observable<JobTaskAlertDto[]> {
    const params = new HttpParams().set('thresholdMinutes', thresholdMinutes.toString());
    return this.http.get<JobTaskAlertDto[]>(`${this.baseUrl}/tasks/unacknowledged-alerts`, {
      params,
    });
  }

  checkAreaEligibility(
    driverId: string,
    areaId: string,
    scheduledDate: string,
  ): Observable<AreaEligibilityCheckDto> {
    const params = new HttpParams()
      .set('driverId', driverId)
      .set('areaId', areaId)
      .set('scheduledDate', scheduledDate);
    return this.http.get<AreaEligibilityCheckDto>(`${this.baseUrl}/check-area-eligibility`, {
      params,
    });
  }

  getTaskById(id: string): Observable<JobTaskDetailDto> {
    return this.http.get<JobTaskDetailDto>(`${this.baseUrl}/tasks/${id}`);
  }

  createTask(request: CreateJobTaskRequest): Observable<JobTaskDetailDto> {
    return this.http.post<JobTaskDetailDto>(`${this.baseUrl}/tasks`, request);
  }

  assignTask(id: string, request: AssignJobTaskRequest): Observable<JobTaskDetailDto> {
    return this.http.post<JobTaskDetailDto>(`${this.baseUrl}/tasks/${id}/assign`, request);
  }

  acknowledgeTask(id: string, request?: AcknowledgeJobTaskRequest): Observable<JobTaskDetailDto> {
    return this.http.post<JobTaskDetailDto>(`${this.baseUrl}/tasks/${id}/acknowledge`, request ?? {});
  }

  startTask(id: string, request?: StartJobTaskRequest): Observable<JobTaskDetailDto> {
    return this.http.post<JobTaskDetailDto>(`${this.baseUrl}/tasks/${id}/start`, request ?? {});
  }

  completeTask(id: string, request?: CompleteJobTaskRequest): Observable<JobTaskDetailDto> {
    return this.http.post<JobTaskDetailDto>(`${this.baseUrl}/tasks/${id}/complete`, request ?? {});
  }

  cancelTask(id: string, request: CancelJobTaskRequest): Observable<JobTaskDetailDto> {
    return this.http.post<JobTaskDetailDto>(`${this.baseUrl}/tasks/${id}/cancel`, request);
  }

  getDrivers(): Observable<PaginatedResult<DriverOption>> {
    const params = new HttpParams().set('pageSize', '100');
    return this.http.get<PaginatedResult<DriverOption>>('/api/drivers', { params });
  }

  getVehicles(): Observable<PaginatedResult<VehicleOption>> {
    const params = new HttpParams().set('pageSize', '100');
    return this.http.get<PaginatedResult<VehicleOption>>('/api/vehicles', { params });
  }

  getAreas(): Observable<PaginatedResult<AreaOption>> {
    const params = new HttpParams().set('pageSize', '100');
    return this.http.get<PaginatedResult<AreaOption>>('/api/areas', { params });
  }
}
