export type SyncStatus = 'online' | 'offline' | 'reconnecting' | 'synced';

export type QueueItemStatus = 'pending' | 'syncing' | 'failed' | 'completed';

export interface OfflineQueueItem {
  id: string;
  clientRequestId: string;
  url: string;
  method: 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: unknown;
  headers?: Record<string, string>;
  createdAt: string;
  retryCount: number;
  status: QueueItemStatus;
  lastError?: string;
  description?: string;
}

export interface CachedRecord<T = unknown> {
  key: string;
  data: T;
  cachedAt: string;
}
