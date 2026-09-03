import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  PayPeriodDto,
  PayslipDto,
  CreatePayPeriodRequest,
  CalculatePayrollRequest,
  VoidPayPeriodRequest,
  PayPeriodFilterParams,
} from '../models/payroll.models';
import { PaginatedList } from '../../../../core/api/models/api-models';

@Injectable({
  providedIn: 'root',
})
export class PayrollService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/payroll';

  getPayPeriods(params?: PayPeriodFilterParams): Observable<PaginatedList<PayPeriodDto>> {
    let httpParams = new HttpParams();
    if (params?.fromDate) httpParams = httpParams.set('fromDate', params.fromDate);
    if (params?.toDate) httpParams = httpParams.set('toDate', params.toDate);
    if (params?.status) httpParams = httpParams.set('status', params.status);
    if (params?.page) httpParams = httpParams.set('page', params.page);
    if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);

    return this.http.get<PaginatedList<PayPeriodDto>>(`${this.baseUrl}/periods`, {
      params: httpParams,
    });
  }

  getPayPeriodById(id: string): Observable<PayPeriodDto> {
    return this.http.get<PayPeriodDto>(`${this.baseUrl}/periods/${id}`);
  }

  createPayPeriod(request: CreatePayPeriodRequest): Observable<PayPeriodDto> {
    return this.http.post<PayPeriodDto>(`${this.baseUrl}/periods`, request);
  }

  calculatePayroll(
    periodId: string,
    request?: CalculatePayrollRequest,
  ): Observable<void> {
    return this.http.post<void>(
      `${this.baseUrl}/periods/${periodId}/calculate`,
      request || {},
    );
  }

  finalisePayPeriod(periodId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/periods/${periodId}/finalise`, {});
  }

  voidPayPeriod(periodId: string, request: VoidPayPeriodRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/periods/${periodId}/void`, request);
  }

  getPayPeriodPayslips(periodId: string): Observable<PayslipDto[]> {
    return this.http.get<PayslipDto[]>(`${this.baseUrl}/periods/${periodId}/payslips`);
  }

  getPayslipById(id: string): Observable<PayslipDto> {
    return this.http.get<PayslipDto>(`${this.baseUrl}/payslips/${id}`);
  }
}
