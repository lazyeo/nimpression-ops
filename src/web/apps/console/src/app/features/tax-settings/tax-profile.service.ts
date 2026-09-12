import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import {
  TaxProfile,
  TaxProfilePage,
  SubmitTaxProfile,
  ApproveTaxProfile,
  TaxProfileStatus,
} from '../../core/payroll/tax-profile.models';
import { PayFrequency } from '../../core/payroll/settlement.models';
import { PayslipDto } from '../admin/payroll/models/payroll.models';
@Injectable({ providedIn: 'root' })
export class TaxProfileService {
  private readonly http = inject(HttpClient);
  mine(page = 1, status?: TaxProfileStatus) {
    let params = new HttpParams().set('page', page).set('pageSize', 20);
    if (status) params = params.set('status', status);
    return this.http.get<TaxProfilePage>('/api/payroll/tax-profiles/mine', { params });
  }
  all(page = 1, status?: TaxProfileStatus) {
    let params = new HttpParams().set('page', page).set('pageSize', 20);
    if (status) params = params.set('status', status);
    return this.http.get<TaxProfilePage>('/api/payroll/tax-profiles', { params });
  }
  submit(request: SubmitTaxProfile) {
    return this.http.post<TaxProfile>('/api/payroll/tax-profiles/mine', request);
  }
  withdraw(id: string) {
    return this.http.post<TaxProfile>(`/api/payroll/tax-profiles/${id}/withdraw`, {});
  }
  approve(id: string, request: ApproveTaxProfile) {
    return this.http.post<TaxProfile>(`/api/payroll/tax-profiles/${id}/approve`, request);
  }
  reject(id: string) {
    return this.http.post<TaxProfile>(`/api/payroll/tax-profiles/${id}/reject`, {});
  }
  forPayslip(id: string, payDate: string) {
    return this.http.get<TaxProfile | null>(`/api/payroll/payslips/${id}/tax-profile`, {
      params: new HttpParams().set('payDate', payDate),
    });
  }
  calculate(id: string, request: { payDate: string; frequency: PayFrequency; profileId: string }) {
    return this.http.post<PayslipDto>(
      `/api/payroll/payslips/${id}/settlement-from-profile`,
      request,
    );
  }
}
