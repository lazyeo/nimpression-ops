import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  ShiftEntryDto,
  TimesheetSummaryDto,
  AdminCorrectShiftRequest,
  TimesheetFilterParams,
  DriverOption,
} from '../models/timesheets.models';
import { PaginatedList } from '../../../../core/api/models/api-models';

@Injectable({
  providedIn: 'root',
})
export class TimesheetsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/timesheets';

  getTimesheets(params?: TimesheetFilterParams): Observable<PaginatedList<ShiftEntryDto>> {
    let httpParams = new HttpParams();
    if (params?.driverId) httpParams = httpParams.set('driverId', params.driverId);
    if (params?.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params?.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params?.status) httpParams = httpParams.set('status', params.status);
    if (params?.page) httpParams = httpParams.set('page', params.page);
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);

    return this.http.get<PaginatedList<ShiftEntryDto>>(`${this.baseUrl}`, {
      params: httpParams,
    });
  }

  getShiftById(id: string): Observable<ShiftEntryDto> {
    return this.http.get<ShiftEntryDto>(`${this.baseUrl}/${id}`);
  }

  getTimesheetSummary(
    driverId?: string,
    fromDate?: string,
    toDate?: string,
  ): Observable<TimesheetSummaryDto> {
    let httpParams = new HttpParams();
    if (driverId) httpParams = httpParams.set('driverId', driverId);
    if (fromDate) httpParams = httpParams.set('fromDate', fromDate);
    if (toDate) httpParams = httpParams.set('toDate', toDate);

    return this.http.get<TimesheetSummaryDto>(`${this.baseUrl}/summary`, {
      params: httpParams,
    });
  }

  adminCorrectShift(
    shiftId: string,
    request: AdminCorrectShiftRequest,
  ): Observable<ShiftEntryDto> {
    return this.http.post<ShiftEntryDto>(
      `${this.baseUrl}/${shiftId}/admin-correct`,
      request,
    );
  }

  getDrivers(): Observable<PaginatedList<DriverOption>> {
    const params = new HttpParams().set('page', 1).set('pageSize', 100);
    return this.http.get<PaginatedList<DriverOption>>('/api/drivers', { params });
  }
}
