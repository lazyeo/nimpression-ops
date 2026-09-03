import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AuditDiffModalComponent } from './audit-diff-modal.component';
import { AuditService } from '../../services/audit.service';
import { I18nService } from '../../../../../core/i18n/i18n.service';
import { AuditEventDto } from '../../models/audit.models';

describe('AuditDiffModalComponent (Visual Diff Inspector)', () => {
  let component: AuditDiffModalComponent;
  let fixture: ComponentFixture<AuditDiffModalComponent>;

  const mockEvent: AuditEventDto = {
    id: 'audit-event-1',
    action: 'Update',
    entityType: 'Vehicle',
    entityId: 'veh-555',
    occurredAt: '2026-09-03T08:00:00Z',
    actorUserId: 'usr-admin',
    actorRole: 'Admin',
    beforeJson: JSON.stringify({
      rego: 'ABC-123',
      odometerKm: 50000,
      status: 'Active',
    }),
    afterJson: JSON.stringify({
      rego: 'ABC-123',
      odometerKm: 55000,
      status: 'Maintenance',
      serviceNotes: 'Oil changed',
    }),
    ipAddress: '10.0.0.1',
  };

  beforeEach(async () => {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AuditDiffModalComponent],
      providers: [
        AuditService,
        I18nService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AuditDiffModalComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('event', mockEvent);
    fixture.detectChanges();
  });

  it('renders readable diff table highlighting modified and added fields', () => {
    const diffs = component.diffItems();
    expect(diffs.length).toBe(4);

    const summary = component.diffSummary();
    expect(summary.modified).toBe(2); // odometerKm, status
    expect(summary.added).toBe(1); // serviceNotes
    expect(summary.unchanged).toBe(1); // rego

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.diff-table')).toBeTruthy();
    // Default hides unchanged fields: 2 modified + 1 added = 3 rows
    expect(compiled.querySelectorAll('.diff-row').length).toBe(3);
  });

  it('toggles visibility of unchanged fields', () => {
    expect(component.showUnchanged()).toBe(false);
    expect(component.visibleDiffItems().length).toBe(3);

    component.toggleUnchanged();
    fixture.detectChanges();

    expect(component.showUnchanged()).toBe(true);
    expect(component.visibleDiffItems().length).toBe(4);
  });

  it('switches to raw JSON side-by-side view', () => {
    expect(component.activeView()).toBe('diff');

    component.setView('raw');
    fixture.detectChanges();

    expect(component.activeView()).toBe('raw');
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.raw-json-grid')).toBeTruthy();
    expect(component.formattedBeforeJson()).toContain('"rego": "ABC-123"');
  });

  it('emits closeModal when close is invoked', () => {
    let closed = false;
    component.closeModal.subscribe(() => {
      closed = true;
    });

    component.close();
    expect(closed).toBe(true);
  });
});
