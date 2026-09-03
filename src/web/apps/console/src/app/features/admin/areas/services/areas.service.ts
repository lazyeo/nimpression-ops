import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AreaAssignmentDto,
  AreaDetailDto,
  AreaDto,
  AreaFilter,
  AssignDriverToAreaRequest,
  CreateAreaRequest,
  EndAreaAssignmentRequest,
  PaginatedResult,
  UpdateAreaRequest,
} from '../models/areas.models';

export interface DriverOption {
  id: string;
  displayName: string;
  employeeNo: string;
  status: string;
}

@Injectable({
  providedIn: 'root',
})
export class AreasService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/areas';

  getAreas(filter: AreaFilter = {}): Observable<PaginatedResult<AreaDto>> {
    let params = new HttpParams();
    if (filter.searchTerm) params = params.set('searchTerm', filter.searchTerm);
    if (filter.isActive !== undefined) params = params.set('isActive', filter.isActive.toString());
    if (filter.page) params = params.set('page', filter.page.toString());
    if (filter.pageSize) params = params.set('pageSize', filter.pageSize.toString());

    return this.http.get<PaginatedResult<AreaDto>>(`${this.baseUrl}`, { params });
  }

  getAreaById(id: string): Observable<AreaDetailDto> {
    return this.http.get<AreaDetailDto>(`${this.baseUrl}/${id}`);
  }

  createArea(request: CreateAreaRequest): Observable<AreaDto> {
    return this.http.post<AreaDto>(`${this.baseUrl}`, request);
  }

  updateArea(id: string, request: UpdateAreaRequest): Observable<AreaDto> {
    return this.http.put<AreaDto>(`${this.baseUrl}/${id}`, request);
  }

  deleteArea(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  assignDriverToArea(
    areaId: string,
    request: AssignDriverToAreaRequest,
  ): Observable<AreaAssignmentDto> {
    return this.http.post<AreaAssignmentDto>(`${this.baseUrl}/${areaId}/assignments`, request);
  }

  endAreaAssignment(
    assignmentId: string,
    request: EndAreaAssignmentRequest,
  ): Observable<AreaAssignmentDto> {
    return this.http.post<AreaAssignmentDto>(
      `${this.baseUrl}/assignments/${assignmentId}/end`,
      request,
    );
  }

  getAreaAssignments(areaId: string, driverId?: string): Observable<AreaAssignmentDto[]> {
    let params = new HttpParams();
    if (driverId) params = params.set('driverId', driverId);
    return this.http.get<AreaAssignmentDto[]>(`${this.baseUrl}/${areaId}/assignments`, {
      params,
    });
  }

  getAllAreaAssignments(areaId?: string, driverId?: string): Observable<AreaAssignmentDto[]> {
    let params = new HttpParams();
    if (areaId) params = params.set('areaId', areaId);
    if (driverId) params = params.set('driverId', driverId);
    return this.http.get<AreaAssignmentDto[]>(`${this.baseUrl}/assignments`, { params });
  }

  getDrivers(): Observable<PaginatedResult<DriverOption>> {
    const params = new HttpParams().set('pageSize', '100');
    return this.http.get<PaginatedResult<DriverOption>>('/api/drivers', { params });
  }
}
