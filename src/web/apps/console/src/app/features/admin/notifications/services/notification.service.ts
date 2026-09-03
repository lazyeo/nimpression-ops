import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  CreateEmailTemplateRequest,
  CreatePartnerContactRequest,
  EmailLogDto,
  EmailLogFilter,
  EmailTemplateDto,
  EmailTemplateFilter,
  PagedResult,
  PartnerContactDto,
  PartnerContactFilter,
  UpdateEmailTemplateRequest,
  UpdatePartnerContactRequest,
} from '../models/notification.models';

@Injectable({
  providedIn: 'root',
})
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/notifications';

  // ── 1. Partner Contacts ─────────────────────────────────────────
  getPartners(filter?: PartnerContactFilter): Observable<PagedResult<PartnerContactDto>> {
    let params = new HttpParams();
    if (filter?.kind !== undefined && filter?.kind !== null) {
      params = params.set('kind', filter.kind.toString());
    }
    if (filter?.active !== undefined && filter?.active !== null) {
      params = params.set('active', filter.active.toString());
    }
    if (filter?.searchTerm) {
      params = params.set('searchTerm', filter.searchTerm);
    }
    if (filter?.page) {
      params = params.set('page', filter.page.toString());
    }
    if (filter?.pageSize) {
      params = params.set('pageSize', filter.pageSize.toString());
    }

    return this.http.get<PagedResult<PartnerContactDto>>(`${this.baseUrl}/partner-contacts`, {
      params,
    });
  }

  getPartnerById(id: string): Observable<PartnerContactDto> {
    return this.http.get<PartnerContactDto>(`${this.baseUrl}/partner-contacts/${id}`);
  }

  createPartner(request: CreatePartnerContactRequest): Observable<PartnerContactDto> {
    return this.http.post<PartnerContactDto>(`${this.baseUrl}/partner-contacts`, request);
  }

  updatePartner(id: string, request: UpdatePartnerContactRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/partner-contacts/${id}`, request);
  }

  activatePartner(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/partner-contacts/${id}/activate`, {});
  }

  deactivatePartner(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/partner-contacts/${id}/deactivate`, {});
  }

  deletePartner(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/partner-contacts/${id}`);
  }

  // ── 2. Email Templates ──────────────────────────────────────────
  getTemplates(filter?: EmailTemplateFilter): Observable<PagedResult<EmailTemplateDto>> {
    let params = new HttpParams();
    if (filter?.searchTerm) {
      params = params.set('searchTerm', filter.searchTerm);
    }
    if (filter?.active !== undefined && filter?.active !== null) {
      params = params.set('active', filter.active.toString());
    }
    if (filter?.page) {
      params = params.set('page', filter.page.toString());
    }
    if (filter?.pageSize) {
      params = params.set('pageSize', filter.pageSize.toString());
    }

    return this.http.get<PagedResult<EmailTemplateDto>>(`${this.baseUrl}/templates`, { params });
  }

  getTemplateById(id: string): Observable<EmailTemplateDto> {
    return this.http.get<EmailTemplateDto>(`${this.baseUrl}/templates/${id}`);
  }

  getTemplateByKey(key: string): Observable<EmailTemplateDto> {
    return this.http.get<EmailTemplateDto>(`${this.baseUrl}/templates/by-key/${key}`);
  }

  createTemplate(request: CreateEmailTemplateRequest): Observable<EmailTemplateDto> {
    return this.http.post<EmailTemplateDto>(`${this.baseUrl}/templates`, request);
  }

  updateTemplate(id: string, request: UpdateEmailTemplateRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/templates/${id}`, request);
  }

  activateTemplate(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/templates/${id}/activate`, {});
  }

  deactivateTemplate(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/templates/${id}/deactivate`, {});
  }

  // ── 3. Email Logs ───────────────────────────────────────────────
  getEmailLogs(filter?: EmailLogFilter): Observable<PagedResult<EmailLogDto>> {
    let params = new HttpParams();
    if (filter?.status) {
      params = params.set('status', filter.status);
    }
    if (filter?.templateKey) {
      params = params.set('templateKey', filter.templateKey);
    }
    if (filter?.toAddress) {
      params = params.set('toAddress', filter.toAddress);
    }
    if (filter?.correlationId) {
      params = params.set('correlationId', filter.correlationId);
    }
    if (filter?.fromDate) {
      params = params.set('fromDate', filter.fromDate);
    }
    if (filter?.toDate) {
      params = params.set('toDate', filter.toDate);
    }
    if (filter?.searchTerm) {
      params = params.set('searchTerm', filter.searchTerm);
    }
    if (filter?.page) {
      params = params.set('page', filter.page.toString());
    }
    if (filter?.pageSize) {
      params = params.set('pageSize', filter.pageSize.toString());
    }

    return this.http.get<PagedResult<EmailLogDto>>(`${this.baseUrl}/logs`, { params });
  }

  getEmailLogById(id: string): Observable<EmailLogDto> {
    return this.http.get<EmailLogDto>(`${this.baseUrl}/logs/${id}`);
  }

  resendEmail(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logs/${id}/resend`, {});
  }

  // ── 4. Compliance Expiry Scan ──────────────────────────────────
  triggerComplianceScan(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/compliance/scan`, {});
  }
}
