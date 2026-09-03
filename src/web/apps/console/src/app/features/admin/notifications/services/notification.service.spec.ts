import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NotificationService } from './notification.service';
import { PartnerKind } from '../models/notification.models';

describe('NotificationService', () => {
  let service: NotificationService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [NotificationService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(NotificationService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('fetches partner contacts with kind filter', () => {
    service.getPartners({ kind: 'Insurer', page: 1, pageSize: 20 }).subscribe((res) => {
      expect(res.items.length).toBe(1);
      expect(res.items[0].companyName).toBe('AA Insurance');
    });

    const req = httpMock.expectOne((r) => r.url === '/api/notifications/partner-contacts');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('kind')).toBe('Insurer');
    req.flush({
      items: [
        {
          id: 'p-1',
          kind: 'Insurer',
          companyName: 'AA Insurance',
          email: 'claims@aainsurance.co.nz',
          active: true,
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
  });

  it('fetches email templates and handles template creation', () => {
    service
      .createTemplate({
        key: 'SERVICE_DUE_REMINDER',
        subjectEn: 'Vehicle Service Due {{VehicleRego}}',
        subjectZh: '车辆维保提醒 {{VehicleRego}}',
        bodyEn: 'Odometer is at {{CurrentOdometer}} km.',
        bodyZh: '当前里程为 {{CurrentOdometer}} 公里。',
        active: true,
      })
      .subscribe((res) => {
        expect(res.key).toBe('SERVICE_DUE_REMINDER');
      });

    const req = httpMock.expectOne('/api/notifications/templates');
    expect(req.request.method).toBe('POST');
    req.flush({
      id: 'tmpl-1',
      key: 'SERVICE_DUE_REMINDER',
      subjectEn: 'Vehicle Service Due {{VehicleRego}}',
      subjectZh: '车辆维保提醒 {{VehicleRego}}',
      bodyEn: 'Odometer is at {{CurrentOdometer}} km.',
      bodyZh: '当前里程为 {{CurrentOdometer}} 公里。',
      active: true,
    });
  });

  it('fetches email logs and triggers manual resend', () => {
    service.getEmailLogs({ status: 'Failed', page: 1, pageSize: 20 }).subscribe((res) => {
      expect(res.items.length).toBe(1);
      expect(res.items[0].status).toBe('Failed');
      expect(res.items[0].attempts).toBe(3);
    });

    const req = httpMock.expectOne((r) => r.url === '/api/notifications/logs');
    expect(req.request.params.get('status')).toBe('Failed');
    req.flush({
      items: [
        {
          id: 'log-1',
          templateKey: 'COMPLIANCE_EXPIRY_WARNING',
          toAddress: 'inspection@vtnz.co.nz',
          subject: 'COF Expiry Warning',
          status: 'Failed',
          attempts: 3,
          lastError: 'SMTP timeout after 30s',
          sentAt: null,
          triggeredBy: 'ComplianceScanner',
          correlationId: 'corr-101',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });

    // Test resend
    service.resendEmail('log-1').subscribe();
    const resendReq = httpMock.expectOne('/api/notifications/logs/log-1/resend');
    expect(resendReq.request.method).toBe('POST');
    resendReq.flush({});
  });

  it('triggers compliance expiry scan', () => {
    service.triggerComplianceScan().subscribe();
    const req = httpMock.expectOne('/api/notifications/compliance/scan');
    expect(req.request.method).toBe('POST');
    req.flush({});
  });
});
