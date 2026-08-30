import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { OfflineQueueItem, QueueItemStatus, SyncStatus } from '../models/offline.models';
import { IndexedDbService, STORES } from './indexed-db.service';

@Injectable({
  providedIn: 'root',
})
export class OfflineQueueService {
  private readonly http = inject(HttpClient, { optional: true });
  private readonly indexedDb = inject(IndexedDbService);

  readonly isOnline = signal<boolean>(typeof navigator !== 'undefined' ? navigator.onLine : true);
  readonly syncStatus = signal<SyncStatus>(this.isOnline() ? 'synced' : 'offline');
  readonly queueItems = signal<OfflineQueueItem[]>([]);
  readonly isReplaying = signal<boolean>(false);

  readonly pendingCount = computed(
    () =>
      this.queueItems().filter((item) => item.status === 'pending' || item.status === 'syncing')
        .length,
  );

  readonly failedCount = computed(
    () => this.queueItems().filter((item) => item.status === 'failed').length,
  );

  readonly hasFailures = computed(() => this.failedCount() > 0);

  constructor() {
    this.initNetworkListeners();
    void this.loadPersistedQueue();
  }

  private initNetworkListeners(): void {
    if (typeof window === 'undefined') return;

    window.addEventListener('online', () => {
      this.isOnline.set(true);
      if (this.queueItems().length > 0) {
        this.syncStatus.set('reconnecting');
        void this.replayQueue();
      } else {
        this.syncStatus.set('synced');
      }
    });

    window.addEventListener('offline', () => {
      this.isOnline.set(false);
      this.syncStatus.set('offline');
    });
  }

  async loadPersistedQueue(): Promise<OfflineQueueItem[]> {
    const items = await this.indexedDb.getAll<OfflineQueueItem>(STORES.OFFLINE_QUEUE);
    this.queueItems.set(items);
    if (!this.isOnline()) {
      this.syncStatus.set('offline');
    } else if (items.some((i) => i.status === 'failed')) {
      this.syncStatus.set('offline');
    } else if (items.length > 0) {
      void this.replayQueue();
    } else {
      this.syncStatus.set('synced');
    }
    return items;
  }

  async enqueue(options: {
    url: string;
    method: 'POST' | 'PUT' | 'PATCH' | 'DELETE';
    body?: unknown;
    headers?: Record<string, string>;
    description?: string;
    customRequestId?: string;
  }): Promise<OfflineQueueItem> {
    const clientRequestId = options.customRequestId || this.generateUuid();
    const item: OfflineQueueItem = {
      id: this.generateUuid(),
      clientRequestId,
      url: options.url,
      method: options.method,
      body: options.body,
      headers: options.headers,
      createdAt: new Date().toISOString(),
      retryCount: 0,
      status: 'pending',
      description: options.description,
    };

    await this.indexedDb.put(STORES.OFFLINE_QUEUE, item);
    this.queueItems.update((list) => [...list, item]);

    if (this.isOnline()) {
      void this.replayQueue();
    } else {
      this.syncStatus.set('offline');
    }

    return item;
  }

  async replayQueue(): Promise<void> {
    if (this.isReplaying()) return;
    if (!this.http) return;

    this.isReplaying.set(true);
    this.syncStatus.set('reconnecting');

    const items = [...this.queueItems()];
    const itemsToProcess = items.filter((i) => i.status === 'pending' || i.status === 'failed');

    for (const item of itemsToProcess) {
      await this.processItem(item);
    }

    this.isReplaying.set(false);

    if (this.failedCount() > 0) {
      this.syncStatus.set('offline');
    } else if (this.isOnline()) {
      this.syncStatus.set('synced');
    }
  }

  async retryItem(id: string): Promise<boolean> {
    const item = this.queueItems().find((i) => i.id === id);
    if (!item) return false;

    item.status = 'pending' as QueueItemStatus;
    await this.indexedDb.put(STORES.OFFLINE_QUEUE, item);
    this.queueItems.update((list) => list.map((i) => (i.id === id ? { ...item } : i)));

    await this.processItem(item);
    return (item.status as QueueItemStatus) === 'completed';
  }

  async retryAll(): Promise<void> {
    const updated = this.queueItems().map((item) => {
      if (item.status === 'failed') {
        return { ...item, status: 'pending' as QueueItemStatus };
      }
      return item;
    });

    for (const item of updated) {
      await this.indexedDb.put(STORES.OFFLINE_QUEUE, item);
    }

    this.queueItems.set(updated);
    await this.replayQueue();
  }

  private async processItem(item: OfflineQueueItem): Promise<void> {
    if (!this.http) return;

    // Update status to syncing
    item.status = 'syncing';
    this.updateItemInState(item);

    const headers = new HttpHeaders({
      ...(item.headers || {}),
      'X-Client-Request-Id': item.clientRequestId,
      ClientRequestId: item.clientRequestId,
    });

    try {
      let req$;
      switch (item.method) {
        case 'POST':
          req$ = this.http.post(item.url, item.body, { headers });
          break;
        case 'PUT':
          req$ = this.http.put(item.url, item.body, { headers });
          break;
        case 'PATCH':
          req$ = this.http.patch(item.url, item.body, { headers });
          break;
        case 'DELETE':
          req$ = this.http.delete(item.url, { headers });
          break;
      }

      await firstValueFrom(req$);

      // Successfully sent -> remove from IndexedDB and memory state
      item.status = 'completed';
      await this.indexedDb.delete(STORES.OFFLINE_QUEUE, item.id);
      this.queueItems.update((list) => list.filter((i) => i.id !== item.id));
    } catch (err) {
      const httpErr = err as HttpErrorResponse;

      // Check if 409 Conflict already processed (idempotent duplicate success)
      if (httpErr?.status === 409) {
        item.status = 'completed';
        await this.indexedDb.delete(STORES.OFFLINE_QUEUE, item.id);
        this.queueItems.update((list) => list.filter((i) => i.id !== item.id));
        return;
      }

      // CRITICAL REQUIREMENT: NO SILENT DISCARD
      // Retain failed items in IndexedDB and UI queue for user inspection and manual retry
      item.status = 'failed';
      item.retryCount += 1;
      item.lastError = httpErr?.message || httpErr?.statusText || 'Replay Failed';

      await this.indexedDb.put(STORES.OFFLINE_QUEUE, item);
      this.updateItemInState(item);
    }
  }

  private updateItemInState(item: OfflineQueueItem): void {
    this.queueItems.update((list) => list.map((i) => (i.id === item.id ? { ...item } : i)));
  }

  private generateUuid(): string {
    if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
      return crypto.randomUUID();
    }
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
      const r = (Math.random() * 16) | 0;
      const v = c === 'x' ? r : (r & 0x3) | 0x8;
      return v.toString(16);
    });
  }
}
