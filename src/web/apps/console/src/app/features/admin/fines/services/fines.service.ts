import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  FineDto,
  FineDetailDto,
  SubmitFineRequest,
  AcceptFineRequest,
  DisputeFineRequest,
  WaiveFineRequest,
  FineFilterParams,
} from '../models/fines.models';
import { PaginatedList, VehicleDto } from '../../../../core/api/models/api-models';

@Injectable({
  providedIn: 'root',
})
export class FinesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/fines';

  getFines(params?: FineFilterParams): Observable<PaginatedList<FineDto>> {
    let httpParams = new HttpParams();
    if (params?.driverId) httpParams = httpParams.set('driverId', params.driverId);
    if (params?.vehicleId) httpParams = httpParams.set('vehicleId', params.vehicleId);
    if (params?.status) httpParams = httpParams.set('status', params.status);
    if (params?.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params?.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params?.searchTerm) httpParams = httpParams.set('searchTerm', params.searchTerm);
    if (params?.page) httpParams = httpParams.set('page', params.page);
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);

    return this.http.get<PaginatedList<FineDto>>(`${this.baseUrl}`, { params: httpParams });
  }

  getFineById(id: string): Observable<FineDetailDto> {
    return this.http.get<FineDetailDto>(`${this.baseUrl}/${id}`);
  }

  getFinePhotoUrl(id: string): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.baseUrl}/${id}/photo`);
  }

  submitFine(request: SubmitFineRequest): Observable<FineDto> {
    return this.http.post<FineDto>(`${this.baseUrl}`, request);
  }

  startReview(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/start-review`, {});
  }

  acceptFine(id: string, request?: AcceptFineRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/accept`, request || {});
  }

  disputeFine(id: string, request: DisputeFineRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/dispute`, request);
  }

  waiveFine(id: string, request: WaiveFineRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/waive`, request);
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
