import { describe, it, expect, beforeEach } from 'vitest';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { OfflineStatusComponent } from './offline-status.component';
import { OfflineQueueService } from './offline-queue.service';
import { RealtimeService } from '../realtime/realtime.service';
import { I18nService } from '../i18n/i18n.service';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { RealtimeConnectionState } from '../models/realtime.models';
import { SyncStatus } from '../models/offline.models';

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
    queueItems: signal([]),
    retryItem: async () => {},
    removeItem: async () => {},
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
      OFFLINE: {
        STATUS_ONLINE: 'Online',
        STATUS_OFFLINE: 'Offline',
        STATUS_SYNCING: 'Syncing...',
        STATUS_SYNCED: 'Synced',
        STATUS_DISCONNECTED: 'Disconnected',
        STATUS_RECONNECTING: 'Reconnecting...',
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

  it('honestly renders Syncing/Reconnecting badge when realtime is reconnecting', () => {
    mockIsOnline.set(true);
    mockSyncStatus.set('synced');
    mockConnectionState.set('reconnecting');
    fixture.detectChanges();

    expect(component.effectiveStatus()).toBe('reconnecting');
    const badge = fixture.nativeElement.querySelector('.status-badge');
    expect(badge).not.toBeNull();
    expect(badge.classList.contains('badge-syncing')).toBe(true);
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
});
