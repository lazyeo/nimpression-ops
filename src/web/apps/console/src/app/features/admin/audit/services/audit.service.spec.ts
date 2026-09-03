import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuditService } from './audit.service';

describe('AuditService (Diff Calculation Engine & Endpoints)', () => {
  let service: AuditService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [AuditService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(AuditService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('fetches audit logs with multi-parameter filter', () => {
    service
      .getAuditLogs({ entityType: 'Vehicle', action: 'Update', page: 1, pageSize: 20 })
      .subscribe((res) => {
        expect(res.items.length).toBe(1);
        expect(res.items[0].entityType).toBe('Vehicle');
      });

    const req = httpMock.expectOne((r) => r.url === '/api/audit-logs');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('entityType')).toBe('Vehicle');
    expect(req.request.params.get('action')).toBe('Update');
    req.flush({
      items: [
        {
          id: 'ev-1',
          action: 'Update',
          entityType: 'Vehicle',
          entityId: 'veh-100',
          occurredAt: '2026-09-02T12:00:00Z',
          actorUserId: 'usr-1',
          actorRole: 1,
          beforeJson: '{"odometer": 10000, "status": "Active"}',
          afterJson: '{"odometer": 15000, "status": "Active", "notes": "Serviced"}',
          ipAddress: '127.0.0.1',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
  });

  it('exports audit logs as CSV blob', () => {
    service.exportAuditLogsCsv({ entityType: 'Vehicle' }).subscribe((blob) => {
      expect(blob).toBeInstanceOf(Blob);
    });

    const req = httpMock.expectOne((r) => r.url === '/api/audit-logs/export');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('entityType')).toBe('Vehicle');
    req.flush(new Blob(['id,action,entity_type\n1,Update,Vehicle'], { type: 'text/csv' }));
  });

  describe('computeDiff calculation', () => {
    it('accurately identifies modified, added, removed, and unchanged fields', () => {
      const beforeJson = JSON.stringify({
        odometer: 10000,
        status: 'Active',
        tempField: 'to-be-deleted',
      });

      const afterJson = JSON.stringify({
        odometer: 15000,
        status: 'Active',
        newField: 'freshly-added',
      });

      const diffs = service.computeDiff(beforeJson, afterJson);

      const modifiedItem = diffs.find((d) => d.key === 'odometer');
      expect(modifiedItem).toBeDefined();
      expect(modifiedItem?.changeType).toBe('modified');
      expect(modifiedItem?.formattedBefore).toBe('10000');
      expect(modifiedItem?.formattedAfter).toBe('15000');

      const unchangedItem = diffs.find((d) => d.key === 'status');
      expect(unchangedItem).toBeDefined();
      expect(unchangedItem?.changeType).toBe('unchanged');

      const addedItem = diffs.find((d) => d.key === 'newField');
      expect(addedItem).toBeDefined();
      expect(addedItem?.changeType).toBe('added');
      expect(addedItem?.formattedAfter).toBe('"freshly-added"');

      const removedItem = diffs.find((d) => d.key === 'tempField');
      expect(removedItem).toBeDefined();
      expect(removedItem?.changeType).toBe('removed');
      expect(removedItem?.formattedBefore).toBe('"to-be-deleted"');
    });

    it('handles creation events where beforeJson is null', () => {
      const afterJson = JSON.stringify({ name: 'Alpha', code: 'A1' });
      const diffs = service.computeDiff(null, afterJson);

      expect(diffs.length).toBe(2);
      expect(diffs.every((d) => d.changeType === 'added')).toBe(true);
    });

    it('handles deletion events where afterJson is null', () => {
      const beforeJson = JSON.stringify({ name: 'Alpha', code: 'A1' });
      const diffs = service.computeDiff(beforeJson, null);

      expect(diffs.length).toBe(2);
      expect(diffs.every((d) => d.changeType === 'removed')).toBe(true);
    });
  });
});
