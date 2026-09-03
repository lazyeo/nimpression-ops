export interface AuditEventDto {
  id: string;
  action: string;
  entityType: string;
  entityId: string;
  occurredAt: string;
  actorUserId?: string | null;
  actorRole?: string | number | null;
  beforeJson?: string | null;
  afterJson?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
}

export interface AuditLogFilter {
  actorUserId?: string | null;
  entityType?: string | null;
  entityId?: string | null;
  action?: string | null;
  from?: string | null;
  to?: string | null;
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export type DiffChangeType = 'added' | 'removed' | 'modified' | 'unchanged';

export interface DiffFieldItem {
  key: string;
  changeType: DiffChangeType;
  beforeValue?: unknown;
  afterValue?: unknown;
  formattedBefore?: string;
  formattedAfter?: string;
}
