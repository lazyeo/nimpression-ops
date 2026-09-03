import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  IncidentReportDto,
  IncidentReportDetailDto,
  ReportIncidentRequest,
  IncidentFilterParams,
} from '../models/incidents.models';
import { PaginatedList, VehicleDto } from '../../../../core/api/models/api-models';

@Injectable({
  providedIn: 'root',
})
export class IncidentsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/incidents';

  getIncidents(params?: IncidentFilterParams): Observable<PaginatedList<IncidentReportDto>> {
    let httpParams = new HttpParams();
    if (params?.driverId) httpParams = httpParams.set('driverId', params.driverId);
    if (params?.vehicleId) httpParams = httpParams.set('vehicleId', params.vehicleId);
    if (params?.severity) httpParams = httpParams.set('severity', params.severity);
    if (params?.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params?.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params?.searchTerm) httpParams = httpParams.set('searchTerm', params.searchTerm);
    if (params?.page) httpParams = httpParams.set('page', params.page);
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);

    return this.http.get<PaginatedList<IncidentReportDto>>(`${this.baseUrl}`, {
      params: httpParams,
    });
  }

  getIncidentById(id: string): Observable<IncidentReportDetailDto> {
    return this.http.get<IncidentReportDetailDto>(`${this.baseUrl}/${id}`);
  }

  reportIncident(request: ReportIncidentRequest): Observable<IncidentReportDto> {
    return this.http.post<IncidentReportDto>(`${this.baseUrl}`, request);
  }

  getDrivers(): Observable<PaginatedList<any>> {
    const params = new HttpParams().set('page', 1).set('pageSize', 100);
    return this.http.get<PaginatedList<any>>('/api/drivers', { params });
  }

  getVehicles(): Observable<PaginatedList<VehicleDto>> {
    const params = new HttpParams().set('page', 1).set('pageSize', 100);
    return this.http.get<PaginatedList<VehicleDto>>('/api/vehicles', { params });
  }
}
