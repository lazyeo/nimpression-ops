import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { OfflineStatusComponent } from './offline-status.component';
import { OfflineQueueService } from './offline-queue.service';
import { RealtimeService } from '../realtime/realtime.service';
import { I18nService } from '../i18n/i18n.service';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { RealtimeConnectionState } from '../models/realtime.models';
import { OfflineQueueItem, SyncStatus } from '../models/offline.models';

describe('OfflineStatusComponent', () => {
  let component: OfflineStatusComponent;
  let fixture: ComponentFixture<OfflineStatusComponent>;

  const mockIsOnline = signal<boolean>(true);
  const mockSyncStatus = signal<SyncStatus>('synced');
  const mockPendingCount = signal<number>(0);
  const mockFailedCount = signal<number>(0);
  const mockHasFailures = signal<boolean>(false);
  const mockConnectionState = signal<RealtimeConnectionState>('connected');

  const mockOfflineQueue = {
    isOnline: mockIsOnline,
    syncStatus: mockSyncStatus,
    pendingCount: mockPendingCount,
    failedCount: mockFailedCount,
    hasFailures: mockHasFailures,
    isReplaying: signal<boolean>(false),
    hasTransientFailures: signal<boolean>(false),
    queueItems: signal<OfflineQueueItem[]>([]),
    retryItem: vi.fn(async () => {}),
    removeItem: vi.fn(async () => {}),
    retryAll: async () => {},
  };

  const mockRealtime = {
    connectionState: mockConnectionState,
  };

  let i18nService: I18nService;

  beforeEach(async () => {
    mockIsOnline.set(true);
    mockSyncStatus.set('synced');
    mockPendingCount.set(0);
    mockFailedCount.set(0);
    mockHasFailures.set(false);
    mockConnectionState.set('connected');
    mockOfflineQueue.queueItems.set([]);
    mockOfflineQueue.hasTransientFailures.set(false);
    vi.clearAllMocks();

    await TestBed.configureTestingModule({
      imports: [OfflineStatusComponent],
      providers: [
        I18nService,
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: OfflineQueueService, useValue: mockOfflineQueue },
        { provide: RealtimeService, useValue: mockRealtime },
      ],
    }).compileComponents();

    i18nService = TestBed.inject(I18nService);
    i18nService.setDictionary('en-NZ', {
      DRIVER: { START_TRIP: 'Start Trip' },
      ERRORS: {
        GENERIC: 'This change could not be saved. Please try again.',
        TASK_STATE_CHANGED: 'The task has changed. Refresh your task list.',
      },
      OFFLINE: {
        STATUS_ONLINE: 'Online',
        STATUS_OFFLINE: 'Offline',
        STATUS_SYNCING: 'Syncing...',
        STATUS_SYNCED: 'Synced',
        STATUS_DISCONNECTED: 'Disconnected',
        STATUS_RECONNECTING: 'Reconnecting...',
        OPERATION_GENERIC: 'Saved change',
        STATUS_FAILED: 'Needs attention',
        STATUS_PENDING: 'Waiting to sync',
        STATUS_ATTENTION: 'Action needed',
        STATUS_COMPLETED: 'Saved',
        ERROR_CODE: 'Code: {code}',
        RETRY_ITEM: 'Retry',
        REMOVE_ITEM: 'Remove',
      },
    });

    fixture = TestBed.createComponent(OfflineStatusComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders green Synced badge when fully online and realtime is connected', () => {
    mockIsOnline.set(true);
    mockSyncStatus.set('synced');
    mockConnectionState.set('connected');
    fixture.detectChanges();

    expect(component.effectiveStatus()).toBe('synced');
    const badge = fixture.nativeElement.querySelector('.status-badge');
    expect(badge).not.toBeNull();
    expect(badge.classList.contains('badge-synced')).toBe(true);
    expect(badge.textContent).toContain('Synced');
  });

  it('renders blue Syncing badge when online and offline queue is syncing (normal write operation)', () => {
    mockIsOnline.set(true);
    mockSyncStatus.set('syncing');
    mockConnectionState.set('connected');
    fixture.detectChanges();

    expect(component.effectiveStatus()).toBe('syncing');
    const badge = fixture.nativeElement.querySelector('.status-badge');
    expect(badge).not.toBeNull();
    expect(badge.classList.contains('badge-syncing')).toBe(true);
    expect(badge.classList.contains('badge-synced')).toBe(false);
    expect(badge.textContent).toContain('Syncing...');
  });

  it('renders warning Reconnecting badge when offline queue is reconnecting after network recovery', () => {
    mockIsOnline.set(true);
    mockSyncStatus.set('reconnecting');
    mockConnectionState.set('connected');
    fixture.detectChanges();

    expect(component.effectiveStatus()).toBe('reconnecting');
    const badge = fixture.nativeElement.querySelector('.status-badge');
    expect(badge).not.toBeNull();
    expect(badge.classList.contains('badge-reconnecting')).toBe(true);
    expect(badge.classList.contains('badge-syncing')).toBe(false);
    expect(badge.classList.contains('badge-synced')).toBe(false);
    expect(badge.textContent).toContain('Reconnecting...');
  });

  it('honestly renders Offline/Disconnected badge when realtime is disconnected even if syncStatus is synced (R3 requirement)', () => {
    // Critical verification for R3: Synced indicator must NEVER mask a disconnected realtime state
    mockIsOnline.set(true);
    mockSyncStatus.set('synced');
    mockConnectionState.set('disconnected');
    fixture.detectChanges();

    expect(component.effectiveStatus()).toBe('offline');
    const badge = fixture.nativeElement.querySelector('.status-badge');
    expect(badge).not.toBeNull();
    expect(badge.classList.contains('badge-offline')).toBe(true);
    expect(badge.classList.contains('badge-synced')).toBe(false);
    expect(badge.textContent).toContain('Disconnected');
  });

  it('honestly renders Reconnecting badge when realtime is reconnecting', () => {
    mockIsOnline.set(true);
    mockSyncStatus.set('synced');
    mockConnectionState.set('reconnecting');
    fixture.detectChanges();

    expect(component.effectiveStatus()).toBe('reconnecting');
    const badge = fixture.nativeElement.querySelector('.status-badge');
    expect(badge).not.toBeNull();
    expect(badge.classList.contains('badge-reconnecting')).toBe(true);
    expect(badge.classList.contains('badge-synced')).toBe(false);
    expect(badge.textContent).toContain('Reconnecting...');
  });

  it('renders Offline badge when network is offline', () => {
    mockIsOnline.set(false);
    mockSyncStatus.set('offline');
    mockConnectionState.set('disconnected');
    fixture.detectChanges();

    expect(component.effectiveStatus()).toBe('offline');
    const badge = fixture.nativeElement.querySelector('.status-badge');
    expect(badge).not.toBeNull();
    expect(badge.classList.contains('badge-offline')).toBe(true);
    expect(badge.textContent).toContain('Offline');
  });

  it('never renders legacy technical errors, endpoints, methods or descriptions', () => {
    const item: OfflineQueueItem = {
      id: 'legacy',
      clientRequestId: 'request',
      url: '/api/private/endpoint?secret=raw',
      method: 'PATCH',
      body: { secret: 'payload-private' },
      createdAt: '2026-09-09T00:00:00Z',
      status: 'failed',
      retryCount: 1,
      isPermanentFailure: true,
      description: 'private-description',
      lastError: 'HTTP 422 ServerStackTrace',
      errorCode: 'private-code',
      errorMessageKey: 'private-key',
    };
    mockOfflineQueue.queueItems.set([item]);
    component.openQueueModal();
    fixture.detectChanges();
    const modal: HTMLElement = fixture.nativeElement.querySelector('.modal-card');
    expect(modal.textContent).toContain('Saved change');
    expect(modal.textContent).toContain('Needs attention');
    expect(modal.textContent).toContain('Code: OPS-UNKNOWN');
    expect(modal.textContent).toContain('This change could not be saved. Please try again.');
    for (const raw of [
      item.url,
      item.method,
      item.description!,
      item.lastError!,
      'payload-private',
      'private-code',
      'private-key',
    ]) {
      expect(modal.innerHTML).not.toContain(raw);
    }
    expect(modal.querySelector('.btn-remove')?.textContent).toContain('Remove');
    (modal.querySelector('.btn-remove') as HTMLButtonElement).click();
    expect(mockOfflineQueue.removeItem).toHaveBeenCalledWith('legacy');
  });

  it('names trusted task actions without exposing task identifiers and retains retry', () => {
    const item: OfflineQueueItem = {
      id: 'task-item',
      clientRequestId: 'request',
      url: '/api/dispatch/tasks/private-task-id/status',
      method: 'POST',
      body: { status: 'IN_PROGRESS' },
      createdAt: '2026-09-09T00:00:00Z',
      status: 'failed',
      retryCount: 2,
      description: 'private-description',
      errorCode: 'TASK-001',
      errorMessageKey: 'private-key',
    };
    mockOfflineQueue.queueItems.set([item]);
    component.openQueueModal();
    fixture.detectChanges();
    const modal: HTMLElement = fixture.nativeElement.querySelector('.modal-card');
    expect(modal.textContent).toContain('Start Trip');
    expect(modal.textContent).toContain('The task has changed. Refresh your task list.');
    expect(modal.textContent).toContain('Code: TASK-001');
    expect(modal.innerHTML).not.toContain('private-key');
    expect(modal.innerHTML).not.toContain('private-task-id');
    expect(modal.innerHTML).not.toContain('private-description');
    expect(modal.querySelectorAll('thead th')).toHaveLength(4);
    (modal.querySelector('.btn-retry') as HTMLButtonElement).click();
    expect(mockOfflineQueue.retryItem).toHaveBeenCalledWith('task-item');
    expect(component.operationLabelKey({ ...item, body: { status: 'private-status' } })).toBe(
      'OFFLINE.OPERATION_GENERIC',
    );
  });

  it('shows action needed for persisted failures while connected instead of offline or synced', () => {
    mockSyncStatus.set('offline');
    mockFailedCount.set(1);
    mockHasFailures.set(true);
    fixture.detectChanges();
    expect(component.effectiveStatus()).toBe('attention');
    const badge: HTMLElement = fixture.nativeElement.querySelector('.status-badge');
    expect(badge.textContent).toContain('Action needed');
    expect(badge.classList.contains('badge-attention')).toBe(true);

    mockConnectionState.set('disconnected');
    fixture.detectChanges();
    expect(component.effectiveStatus()).toBe('offline');
    expect(badge.classList.contains('badge-synced')).toBe(false);
  });

  it('does not claim synced while queued changes are waiting', () => {
    mockPendingCount.set(1);
    fixture.detectChanges();
    expect(component.effectiveStatus()).toBe('pending');
    expect(fixture.nativeElement.querySelector('.status-badge').textContent).toContain(
      'Waiting to sync',
    );
  });
});
