import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuditComponent } from './audit.component';
import { AuditService } from './services/audit.service';
import { AuthService } from '../../../core/auth/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { AuditEventDto, PagedResult } from './models/audit.models';

describe('AuditComponent (Append-Only Audit Logs & CSV Export)', () => {
  let component: AuditComponent;
  let fixture: ComponentFixture<AuditComponent>;
  let httpMock: HttpTestingController;

  const mockAuditLogs: AuditEventDto[] = [
    {
      id: 'audit-1',
      action: 'Update',
      entityType: 'Vehicle',
      entityId: 'veh-001',
      occurredAt: '2026-09-02T10:30:00Z',
      actorUserId: 'u-100',
      actorRole: 1,
      beforeJson: '{"status":"Active"}',
      afterJson: '{"status":"Maintenance"}',
      ipAddress: '192.168.1.1',
    },
    {
      id: 'audit-2',
      action: 'Create',
      entityType: 'NewsPost',
      entityId: 'news-001',
      occurredAt: '2026-09-03T09:00:00Z',
      actorUserId: 'u-100',
      actorRole: 1,
      beforeJson: null,
      afterJson: '{"title":"Safety Alert"}',
      ipAddress: '192.168.1.1',
    },
  ];

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AuditComponent],
      providers: [
        AuditService,
        AuthService,
        I18nService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AuditComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('renders audit logs list and displays append-only security banner', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === '/api/audit-logs');
    expect(req.request.method).toBe('GET');

    const mockResponse: PagedResult<AuditEventDto> = {
      items: mockAuditLogs,
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    };
    req.flush(mockResponse);
    fixture.detectChanges();

    expect(component.loading()).toBe(false);
    expect(component.logs().length).toBe(2);

    const compiled = fixture.nativeElement as HTMLElement;
    // Verify append-only notice banner is displayed
    expect(compiled.querySelector('.security-banner')).toBeTruthy();
    // Verify table rendered with 2 rows
    expect(compiled.querySelectorAll('tbody tr').length).toBe(2);
  });

  it('renders empty data state when no audit records are found', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === '/api/audit-logs');
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

  it('applies entityType and action filters and triggers query reload', () => {
    fixture.detectChanges();
    const initialReq = httpMock.expectOne((r) => r.url === '/api/audit-logs');
    initialReq.flush({
      items: mockAuditLogs,
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
    fixture.detectChanges();

    // Set filter
    component.entityTypeFilter.set('Vehicle');
    component.onFilterChange();

    const filterReq = httpMock.expectOne(
      (r) => r.url === '/api/audit-logs' && r.params.get('entityType') === 'Vehicle',
    );
    filterReq.flush({
      items: [mockAuditLogs[0]],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
    fixture.detectChanges();

    expect(component.logs().length).toBe(1);
    expect(component.logs()[0].entityType).toBe('Vehicle');
  });

  it('handles error state and allows retry', () => {
    fixture.detectChanges();

    const req = httpMock.expectOne((r) => r.url === '/api/audit-logs');
    req.flush('Error loading audit records', { status: 500, statusText: 'Error' });
    fixture.detectChanges();

    expect(component.error()).toBeTruthy();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.error-state')).toBeTruthy();

    // Retry
    component.loadAuditLogs();
    const retryReq = httpMock.expectOne((r) => r.url === '/api/audit-logs');
    retryReq.flush({
      items: mockAuditLogs,
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
    fixture.detectChanges();

    expect(component.error()).toBeNull();
    expect(component.logs().length).toBe(2);
  });

  it('opens and closes readable diff modal', () => {
    fixture.detectChanges();
    const req = httpMock.expectOne((r) => r.url === '/api/audit-logs');
    req.flush({
      items: mockAuditLogs,
      totalCount: 2,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
    fixture.detectChanges();

    component.openDiffModal(mockAuditLogs[0]);
    expect(component.selectedEventForDiff()?.id).toBe('audit-1');

    component.closeDiffModal();
    expect(component.selectedEventForDiff()).toBeNull();
  });
});
