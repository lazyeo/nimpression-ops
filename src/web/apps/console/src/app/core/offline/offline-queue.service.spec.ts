import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { OfflineQueueService } from './offline-queue.service';
import { IndexedDbService } from './indexed-db.service';
import { OfflineQueueItem } from '../models/offline.models';

describe('OfflineQueueService (PWA & Offline Reliability)', () => {
  let service: OfflineQueueService;
  let httpMock: HttpTestingController;
  let mockIndexedDb: {
    data: Record<string, OfflineQueueItem>;
    getAll: (store: string) => Promise<OfflineQueueItem[]>;
    put: (store: string, val: OfflineQueueItem) => Promise<void>;
    delete: (store: string, key: string) => Promise<void>;
    get: (store: string, key: string) => Promise<OfflineQueueItem | undefined>;
    clear: (store: string) => Promise<void>;
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    const memoryStore: Record<string, OfflineQueueItem> = {};

    mockIndexedDb = {
      data: memoryStore,
      getAll: async (_store: string) => Object.values(memoryStore),
      put: async (_store: string, val: OfflineQueueItem) => {
        memoryStore[val.id] = { ...val };
      },
      delete: async (_store: string, key: string) => {
        delete memoryStore[key];
      },
      get: async (_store: string, key: string) => memoryStore[key],
      clear: async (_store: string) => {
        for (const k of Object.keys(memoryStore)) delete memoryStore[k];
      },
    };

    TestBed.configureTestingModule({
      providers: [
        OfflineQueueService,
        { provide: IndexedDbService, useValue: mockIndexedDb },
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(OfflineQueueService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('persists queued item to IndexedDB immediately upon enqueue', async () => {
    service.isOnline.set(false);

    const item = await service.enqueue({
      url: '/api/drivers/shift/clock-in',
      method: 'POST',
      body: { driverId: 'd-123' },
      description: 'Clock In',
    });

    expect(item.id).toBeTruthy();
    expect(item.clientRequestId).toBeTruthy();
    expect(mockIndexedDb.data[item.id]).toBeTruthy();
    expect(mockIndexedDb.data[item.id].clientRequestId).toBe(item.clientRequestId);
  });

  it('survives page refresh: recovers persisted queue from IndexedDB on startup (AC: 刷新页面不丢)', async () => {
    const existingItem: OfflineQueueItem = {
      id: 'item-uuid-1',
      clientRequestId: 'req-uuid-1',
      url: '/api/drivers/tasks/1/start',
      method: 'POST',
      body: { status: 'in-progress' },
      createdAt: new Date().toISOString(),
      retryCount: 0,
      status: 'pending',
      description: 'Start Trip',
    };
    mockIndexedDb.data[existingItem.id] = existingItem;

    service.isOnline.set(false);
    const recovered = await service.loadPersistedQueue();

    expect(recovered.length).toBe(1);
    expect(recovered[0].id).toBe('item-uuid-1');
    expect(recovered[0].clientRequestId).toBe('req-uuid-1');
    expect(service.queueItems().length).toBe(1);

    // When online, replayQueue is executed
    service.isOnline.set(true);
    const replayPromise = service.replayQueue();

    const req = httpMock.expectOne('/api/drivers/tasks/1/start');
    expect(req.request.headers.get('X-Client-Request-Id')).toBe('req-uuid-1');
    expect(req.request.headers.get('ClientRequestId')).toBe('req-uuid-1');
    req.flush({ success: true });

    await replayPromise;
    expect(service.queueItems().length).toBe(0);
  });

  it('replays queue with ClientRequestId idempotency headers (AC: 重放带 ClientRequestId)', async () => {
    service.isOnline.set(false);
    const customReqId = 'custom-client-req-999';

    await service.enqueue({
      url: '/api/drivers/tasks/complete',
      method: 'POST',
      body: { taskId: 't-1' },
      customRequestId: customReqId,
    });

    expect(service.queueItems().length).toBe(1);

    service.isOnline.set(true);
    const replayPromise = service.replayQueue();

    const req = httpMock.expectOne('/api/drivers/tasks/complete');
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('X-Client-Request-Id')).toBe(customReqId);
    expect(req.request.headers.get('ClientRequestId')).toBe(customReqId);

    req.flush({ status: 'completed' });
    await replayPromise;

    expect(service.queueItems().length).toBe(0);
  });

  it('prohibits silent discard: retains failed replay in IndexedDB and marks status as failed (AC: 禁止静默丢弃)', async () => {
    service.isOnline.set(false);

    const item = await service.enqueue({
      url: '/api/drivers/tasks/fail-op',
      method: 'POST',
      body: { errorTest: true },
    });

    service.isOnline.set(true);
    const replayPromise = service.replayQueue();

    const req = httpMock.expectOne('/api/drivers/tasks/fail-op');
    req.flush('Internal Server Error', { status: 500, statusText: 'Internal Server Error' });

    await replayPromise;

    // Must NOT be discarded
    expect(service.queueItems().length).toBe(1);
    expect(service.failedCount()).toBe(1);
    expect(service.hasFailures()).toBe(true);

    const retainedInDb = mockIndexedDb.data[item.id];
    expect(retainedInDb).toBeTruthy();
    expect(retainedInDb.status).toBe('failed');
    expect(retainedInDb.retryCount).toBe(1);
    expect(retainedInDb.lastError).toBeTruthy();
  });

  it('handles 409 Conflict as idempotent success and removes from queue', async () => {
    service.isOnline.set(false);

    await service.enqueue({
      url: '/api/drivers/tasks/idempotent-op',
      method: 'POST',
      body: { data: 1 },
    });

    service.isOnline.set(true);
    const replayPromise = service.replayQueue();

    const req = httpMock.expectOne('/api/drivers/tasks/idempotent-op');
    req.flush({ error: 'conflict' }, { status: 409, statusText: 'Conflict' });

    await replayPromise;
    expect(service.queueItems().length).toBe(0);
  });

  it('supports explicit retry of failed items', async () => {
    const failedItem: OfflineQueueItem = {
      id: 'failed-id',
      clientRequestId: 'failed-req-id',
      url: '/api/retry-test',
      method: 'POST',
      createdAt: new Date().toISOString(),
      retryCount: 1,
      status: 'failed',
      lastError: 'Server Timeout',
    };
    mockIndexedDb.data[failedItem.id] = failedItem;

    service.isOnline.set(false);
    await service.loadPersistedQueue();

    expect(service.failedCount()).toBe(1);

    const retryPromise = service.retryItem('failed-id');
    await Promise.resolve();
    await Promise.resolve();

    const req = httpMock.expectOne('/api/retry-test');
    expect(req.request.headers.get('X-Client-Request-Id')).toBe('failed-req-id');
    req.flush({ ok: true });

    const success = await retryPromise;
    expect(success).toBe(true);
    expect(service.failedCount()).toBe(0);
    expect(mockIndexedDb.data['failed-id']).toBeUndefined();
  });

  it('handles 4xx permanent failure: marks as permanent failure, excludes from retryAll, and allows removal (AC 4)', async () => {
    service.isOnline.set(false);

    const item = await service.enqueue({
      url: '/api/dispatch/tasks/invalid-uuid/status',
      method: 'POST',
      body: { status: 'IN_PROGRESS' },
      description: 'Invalid Transition',
    });

    service.isOnline.set(true);
    const replayPromise = service.replayQueue();

    const req = httpMock.expectOne('/api/dispatch/tasks/invalid-uuid/status');
    req.flush(
      {
        error: 'invalid_transition',
        message: 'Cannot transition JobTask from Assigned to InProgress.',
      },
      { status: 422, statusText: 'Unprocessable Entity' },
    );

    await replayPromise;

    // Item must be marked as failed AND isPermanentFailure = true
    expect(service.failedCount()).toBe(1);
    expect(service.permanentFailedCount()).toBe(1);
    expect(service.transientFailedCount()).toBe(0);
    expect(service.hasTransientFailures()).toBe(false);

    const queueItem = service.queueItems()[0];
    expect(queueItem.status).toBe('failed');
    expect(queueItem.isPermanentFailure).toBe(true);
    expect(queueItem.lastError).toBe('Cannot transition JobTask from Assigned to InProgress.');
    expect(queueItem.lastError).not.toContain('Http failure response');
    expect(mockIndexedDb.data[item.id].isPermanentFailure).toBe(true);
    expect(mockIndexedDb.data[item.id].lastError).toBe(
      'Cannot transition JobTask from Assigned to InProgress.',
    );

    // retryAll should NOT retry permanent failures (no HTTP request should be sent)
    await service.retryAll();
    httpMock.expectNone('/api/dispatch/tasks/invalid-uuid/status');
    expect(service.queueItems()[0].status).toBe('failed');

    // User can explicitly remove the permanently failed item
    await service.removeItem(item.id);
    expect(service.queueItems().length).toBe(0);
    expect(service.failedCount()).toBe(0);
    expect(mockIndexedDb.data[item.id]).toBeUndefined();
  });

  it('handles transient 5xx/network failure: marks as transient failure and allows retryAll to replay (AC 4)', async () => {
    service.isOnline.set(false);

    const item = await service.enqueue({
      url: '/api/dispatch/tasks/valid-uuid/status',
      method: 'POST',
      body: { status: 'ACKNOWLEDGED' },
      description: 'Accept Task',
    });

    service.isOnline.set(true);
    const replayPromise = service.replayQueue();

    const req = httpMock.expectOne('/api/dispatch/tasks/valid-uuid/status');
    req.flush('Bad Gateway', { status: 502, statusText: 'Bad Gateway' });

    await replayPromise;

    // Item must be marked as failed AND isPermanentFailure = false
    expect(service.failedCount()).toBe(1);
    expect(service.permanentFailedCount()).toBe(0);
    expect(service.transientFailedCount()).toBe(1);
    expect(service.hasTransientFailures()).toBe(true);

    const queueItem = service.queueItems()[0];
    expect(queueItem.status).toBe('failed');
    expect(queueItem.isPermanentFailure).toBe(false);

    // retryAll should retry transient failures
    const retryAllPromise = service.retryAll();
    await Promise.resolve();
    await Promise.resolve();
    const retryReq = httpMock.expectOne('/api/dispatch/tasks/valid-uuid/status');
    retryReq.flush({ success: true });

    await retryAllPromise;
    expect(service.queueItems().length).toBe(0);
    expect(service.failedCount()).toBe(0);
    expect(mockIndexedDb.data[item.id]).toBeUndefined();
  });
});
