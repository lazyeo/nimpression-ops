import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { EmailLogsTabComponent } from './email-logs-tab.component';
import { NotificationService } from '../../services/notification.service';
import { I18nService } from '../../../../../core/i18n/i18n.service';
import { EmailLogDto, PagedResult } from '../../models/notification.models';

describe('EmailLogsTabComponent (Outbox Delivery Monitor & Manual Resend)', () => {
  let component: EmailLogsTabComponent;
  let fixture: ComponentFixture<EmailLogsTabComponent>;
  let httpMock: HttpTestingController;

  const mockLogs: EmailLogDto[] = [
    {
      id: 'log-sent',
      templateKey: 'SERVICE_DUE_REMINDER',
      toAddress: 'fleet@mechanic.co.nz',
      subject: 'Vehicle Service Due NIM-100',
      status: 'Sent',
      attempts: 1,
      lastError: null,
      sentAt: '2026-09-02T10:00:00Z',
      triggeredBy: 'Scheduler',
      correlationId: 'corr-001',
    },
    {
      id: 'log-failed',
      templateKey: 'COMPLIANCE_EXPIRY_WARNING',
      toAddress: 'testing@vtnz.co.nz',
      subject: 'COF Warning NIM-200',
      status: 'Failed',
      attempts: 3,
      lastError: 'Connection refused: 554 Relay Access Denied',
      sentAt: null,
      triggeredBy: 'ComplianceScanner',
      correlationId: 'corr-002',
    },
  ];

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [EmailLogsTabComponent],
      providers: [
        NotificationService,
        I18nService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(EmailLogsTabComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('renders outbox health cards and log list with retry count', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === '/api/notifications/logs');
    expect(req.request.method).toBe('GET');

    const mockResponse: PagedResult<EmailLogDto> = {
      items: mockLogs,
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    };
    req.flush(mockResponse);
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.logs().length).toBe(2);

    // Check Outbox Overview Metrics
    expect(component.outboxStats().sent).toBe(1);
    expect(component.outboxStats().failed).toBe(1);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.outbox-summary-grid')).toBeTruthy();
    expect(compiled.querySelectorAll('tbody tr').length).toBe(2);
  });

  it('renders empty state when no email logs exist', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === '/api/notifications/logs');
    req.flush({
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.logs().length).toBe(0);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.empty-state')).toBeTruthy();
  });

  it('handles error state and retry for email logs', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === '/api/notifications/logs');
    req.flush('Failed to load logs', { status: 500, statusText: 'Error' });
    fixture.detectChanges();

    expect(component.error()).toBeTruthy();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.error-state')).toBeTruthy();

    // Trigger retry
    component.loadLogs();
    const retryReq = httpMock.expectOne((r) => r.url === '/api/notifications/logs');
    retryReq.flush({
      items: mockLogs,
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
    fixture.detectChanges();

    expect(component.error()).toBeNull();
    expect(component.logs().length).toBe(2);
  });

  it('opens log detail modal with error traceback and triggers manual resend', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne((r) => r.url === '/api/notifications/logs');
    req.flush({
      items: mockLogs,
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
    fixture.detectChanges();

    // Open detail for failed log
    component.openLogDetail(mockLogs[1]);
    fixture.detectChanges();

    expect(component.selectedLog()?.id).toBe('log-failed');
    expect(component.selectedLog()?.lastError).toContain('Relay Access Denied');

    // Trigger resend
    component.resendEmail('log-failed');

    const resendReq = httpMock.expectOne('/api/notifications/logs/log-failed/resend');
    expect(resendReq.request.method).toBe('POST');
    resendReq.flush({});

    // Reloads logs after resend
    const reloadReq = httpMock.expectOne((r) => r.url === '/api/notifications/logs');
    reloadReq.flush({
      items: [
        {
          ...mockLogs[1],
          status: 'Pending',
          attempts: 4,
        },
      ],
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
    fixture.detectChanges();

    expect(component.resendSuccessMsg()).toBeTruthy();
  });
});
