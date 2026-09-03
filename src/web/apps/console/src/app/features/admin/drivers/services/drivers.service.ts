import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CreateDriverRequest,
  DeactivateDriverRequest,
  DriverDetailDto,
  DriverFilter,
  DriverLicenceAlertDto,
  DriverSummaryDto,
  PaginatedResult,
  UpdateDriverRequest,
  UploadAvatarResultDto,
} from '../models/drivers.models';

export interface AreaLookupOption {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class DriversService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/drivers';

  getDrivers(filter: DriverFilter = {}): Observable<PaginatedResult<DriverSummaryDto>> {
    let params = new HttpParams();
    if (filter.searchTerm) params = params.set('searchTerm', filter.searchTerm);
    if (filter.name) params = params.set('name', filter.name);
    if (filter.employeeNo) params = params.set('employeeNo', filter.employeeNo);
    if (filter.status) params = params.set('status', filter.status);
    if (filter.areaId) params = params.set('areaId', filter.areaId);
    if (filter.page) params = params.set('page', filter.page.toString());
    if (filter.pageSize) params = params.set('pageSize', filter.pageSize.toString());

    return this.http.get<PaginatedResult<DriverSummaryDto>>(`${this.baseUrl}`, { params });
  }

  getLicenceAlerts(daysThreshold = 30): Observable<DriverLicenceAlertDto[]> {
    const params = new HttpParams().set('daysThreshold', daysThreshold.toString());
    return this.http.get<DriverLicenceAlertDto[]>(`${this.baseUrl}/licence-alerts`, { params });
  }

  getDriverById(id: string): Observable<DriverDetailDto> {
    return this.http.get<DriverDetailDto>(`${this.baseUrl}/${id}`);
  }

  createDriver(request: CreateDriverRequest): Observable<DriverDetailDto> {
    return this.http.post<DriverDetailDto>(`${this.baseUrl}`, request);
  }

  updateDriver(id: string, request: UpdateDriverRequest): Observable<DriverDetailDto> {
    return this.http.put<DriverDetailDto>(`${this.baseUrl}/${id}`, request);
  }

  deactivateDriver(id: string, request?: DeactivateDriverRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/deactivate`, request ?? {});
  }

  uploadAvatar(id: string, file: File): Observable<UploadAvatarResultDto> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<UploadAvatarResultDto>(`${this.baseUrl}/${id}/avatar`, formData);
  }

  getAreas(): Observable<PaginatedResult<AreaLookupOption>> {
    const params = new HttpParams().set('pageSize', '100');
    return this.http.get<PaginatedResult<AreaLookupOption>>('/api/areas', { params });
  }
}
