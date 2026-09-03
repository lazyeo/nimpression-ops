import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AuditEventDto,
  AuditLogFilter,
  DiffFieldItem,
  PagedResult,
} from '../models/audit.models';

@Injectable({
  providedIn: 'root',
})
export class AuditService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/audit-logs';

  getAuditLogs(filter?: AuditLogFilter): Observable<PagedResult<AuditEventDto>> {
    let params = new HttpParams();
    if (filter?.actorUserId) {
      params = params.set('actorUserId', filter.actorUserId);
    }
    if (filter?.entityType) {
      params = params.set('entityType', filter.entityType);
    }
    if (filter?.entityId) {
      params = params.set('entityId', filter.entityId);
    }
    if (filter?.action) {
      params = params.set('action', filter.action);
    }
    if (filter?.from) {
      params = params.set('from', filter.from);
    }
    if (filter?.to) {
      params = params.set('to', filter.to);
    }
    if (filter?.page) {
      params = params.set('page', filter.page.toString());
    }
    if (filter?.pageSize) {
      params = params.set('pageSize', filter.pageSize.toString());
    }

    return this.http.get<PagedResult<AuditEventDto>>(this.baseUrl, { params });
  }

  exportAuditLogsCsv(filter?: AuditLogFilter): Observable<Blob> {
    let params = new HttpParams();
    if (filter?.actorUserId) {
      params = params.set('actorUserId', filter.actorUserId);
    }
    if (filter?.entityType) {
      params = params.set('entityType', filter.entityType);
    }
    if (filter?.entityId) {
      params = params.set('entityId', filter.entityId);
    }
    if (filter?.action) {
      params = params.set('action', filter.action);
    }
    if (filter?.from) {
      params = params.set('from', filter.from);
    }
    if (filter?.to) {
      params = params.set('to', filter.to);
    }

    return this.http.get(`${this.baseUrl}/export`, {
      params,
      responseType: 'blob',
    });
  }

  /**
   * Computes a structured, human-readable diff between beforeJson and afterJson.
   */
  computeDiff(beforeJson?: string | null, afterJson?: string | null): DiffFieldItem[] {
    let beforeObj: Record<string, unknown> = {};
    let afterObj: Record<string, unknown> = {};

    if (beforeJson && typeof beforeJson === 'string') {
      try {
        beforeObj = JSON.parse(beforeJson) as Record<string, unknown>;
      } catch {
        beforeObj = { raw: beforeJson };
      }
    }

    if (afterJson && typeof afterJson === 'string') {
      try {
        afterObj = JSON.parse(afterJson) as Record<string, unknown>;
      } catch {
        afterObj = { raw: afterJson };
      }
    }

    const allKeys = Array.from(new Set([...Object.keys(beforeObj || {}), ...Object.keys(afterObj || {})])).sort();
    const diffs: DiffFieldItem[] = [];

    for (const key of allKeys) {
      const hasBefore = key in (beforeObj || {});
      const hasAfter = key in (afterObj || {});
      const beforeVal = beforeObj ? beforeObj[key] : undefined;
      const afterVal = afterObj ? afterObj[key] : undefined;

      const formattedBefore = this.formatValue(beforeVal);
      const formattedAfter = this.formatValue(afterVal);

      if (!hasBefore && hasAfter) {
        diffs.push({
          key,
          changeType: 'added',
          afterValue: afterVal,
          formattedAfter,
        });
      } else if (hasBefore && !hasAfter) {
        diffs.push({
          key,
          changeType: 'removed',
          beforeValue: beforeVal,
          formattedBefore,
        });
      } else if (JSON.stringify(beforeVal) !== JSON.stringify(afterVal)) {
        diffs.push({
          key,
          changeType: 'modified',
          beforeValue: beforeVal,
          afterValue: afterVal,
          formattedBefore,
          formattedAfter,
        });
      } else {
        diffs.push({
          key,
          changeType: 'unchanged',
          beforeValue: beforeVal,
          afterValue: afterVal,
          formattedBefore,
          formattedAfter,
        });
      }
    }

    return diffs;
  }

  private formatValue(val: unknown): string {
    if (val === null || val === undefined) return 'null';
    if (typeof val === 'string') return `"${val}"`;
    if (typeof val === 'number' || typeof val === 'boolean') return String(val);
    if (typeof val === 'object') {
      try {
        return JSON.stringify(val, null, 2);
      } catch {
        return String(val);
      }
    }
    return String(val);
  }
}
