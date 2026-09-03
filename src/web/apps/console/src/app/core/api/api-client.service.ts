import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  VehicleDto,
  OdometerReadingDto,
  JobTaskDto,
  TimesheetDto,
  FineDto,
  PayPeriodDto,
  PayslipDto,
  DriverDto,
  PaginatedList,
} from './models/api-models';

@Injectable({
  providedIn: 'root',
})
export class ApiClientService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api';

  getVehicles(params?: {
    search?: string;
    status?: string;
    serviceDueOnly?: boolean;
    page?: number;
    pageSize?: number;
  }): Observable<PaginatedList<VehicleDto>> {
    let httpParams = new HttpParams();
    if (params?.search) httpParams = httpParams.set('search', params.search);
    if (params?.status) httpParams = httpParams.set('status', params.status);
    if (params?.serviceDueOnly !== undefined)
      httpParams = httpParams.set('serviceDueOnly', params.serviceDueOnly);
    if (params?.page) httpParams = httpParams.set('page', params.page);
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);

    return this.http.get<PaginatedList<VehicleDto>>(`${this.baseUrl}/vehicles`, {
      params: httpParams,
    });
  }

  getOdometerReadings(vehicleId: string, limit: number = 50): Observable<OdometerReadingDto[]> {
    const params = new HttpParams().set('limit', limit);
    return this.http.get<OdometerReadingDto[]>(`${this.baseUrl}/vehicles/${vehicleId}/odometer`, {
      params,
    });
  }

  getJobTasks(params?: {
    driverId?: string;
    vehicleId?: string;
    status?: string;
    from?: string;
    to?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PaginatedList<JobTaskDto>> {
    let httpParams = new HttpParams();
    if (params?.driverId) httpParams = httpParams.set('driverId', params.driverId);
    if (params?.vehicleId) httpParams = httpParams.set('vehicleId', params.vehicleId);
    if (params?.status) httpParams = httpParams.set('status', params.status);
    if (params?.from) httpParams = httpParams.set('from', params.from);
    if (params?.to) httpParams = httpParams.set('to', params.to);
    if (params?.page) httpParams = httpParams.set('page', params.page);
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);

    return this.http.get<PaginatedList<JobTaskDto>>(`${this.baseUrl}/dispatch/tasks`, {
      params: httpParams,
    });
  }

  getTimesheets(params?: {
    driverId?: string;
    fromDate?: string;
    toDate?: string;
    status?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PaginatedList<TimesheetDto>> {
    let httpParams = new HttpParams();
    if (params?.driverId) httpParams = httpParams.set('driverId', params.driverId);
    if (params?.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params?.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params?.status) httpParams = httpParams.set('status', params.status);
    if (params?.page) httpParams = httpParams.set('page', params.page);
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);

    return this.http.get<PaginatedList<TimesheetDto>>(`${this.baseUrl}/timesheets`, {
      params: httpParams,
    });
  }

  getFines(params?: {
    driverId?: string;
    vehicleId?: string;
    status?: string;
    fromDate?: string;
    toDate?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PaginatedList<FineDto>> {
    let httpParams = new HttpParams();
    if (params?.driverId) httpParams = httpParams.set('driverId', params.driverId);
    if (params?.vehicleId) httpParams = httpParams.set('vehicleId', params.vehicleId);
    if (params?.status) httpParams = httpParams.set('status', params.status);
    if (params?.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params?.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params?.page) httpParams = httpParams.set('page', params.page);
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);

    return this.http.get<PaginatedList<FineDto>>(`${this.baseUrl}/fines`, { params: httpParams });
  }

  getPayPeriods(params?: {
    fromDate?: string;
    toDate?: string;
    status?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PaginatedList<PayPeriodDto>> {
    let httpParams = new HttpParams();
    if (params?.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params?.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params?.status) httpParams = httpParams.set('status', params.status);
    if (params?.page) httpParams = httpParams.set('page', params.page);
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);

    return this.http.get<PaginatedList<PayPeriodDto>>(`${this.baseUrl}/payroll/periods`, {
      params: httpParams,
    });
  }

  getPayPeriodPayslips(periodId: string): Observable<PayslipDto[]> {
    return this.http.get<PayslipDto[]>(`${this.baseUrl}/payroll/periods/${periodId}/payslips`);
  }

  getDrivers(params?: { page?: number; pageSize?: number }): Observable<PaginatedList<DriverDto>> {
    let httpParams = new HttpParams();
    if (params?.page) httpParams = httpParams.set('page', params.page);
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);

    return this.http.get<PaginatedList<DriverDto>>(`${this.baseUrl}/drivers`, {
      params: httpParams,
    });
  }
}
