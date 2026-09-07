import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SwUpdate, UnrecoverableStateEvent, VersionReadyEvent } from '@angular/service-worker';
import { BehaviorSubject, Subject } from 'rxjs';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { SwUpdateService } from './sw-update.service';

describe('SwUpdateService (W26 R1 Version Updates & Unrecoverable Cache)', () => {
  let service: SwUpdateService;
  let versionUpdates$: Subject<any>;
  let unrecoverable$: Subject<UnrecoverableStateEvent>;
  let isStable$: BehaviorSubject<boolean>;
  let mockSwUpdate: {
    isEnabled: boolean;
    versionUpdates: Subject<any>;
    unrecoverable: Subject<UnrecoverableStateEvent>;
    checkForUpdate: () => Promise<boolean>;
    activateUpdate: () => Promise<boolean>;
  };
  let reloadSpy: any;

  beforeEach(() => {
    TestBed.resetTestingModule();

    versionUpdates$ = new Subject();
    unrecoverable$ = new Subject();
    isStable$ = new BehaviorSubject<boolean>(false);

    mockSwUpdate = {
      isEnabled: true,
      versionUpdates: versionUpdates$,
      unrecoverable: unrecoverable$,
      checkForUpdate: vi.fn().mockResolvedValue(true),
      activateUpdate: vi.fn().mockResolvedValue(true),
    };

    TestBed.configureTestingModule({
      providers: [
        SwUpdateService,
        { provide: SwUpdate, useValue: mockSwUpdate },
        {
          provide: ApplicationRef,
          useValue: {
            isStable: isStable$.asObservable(),
          },
        },
      ],
    });

    service = TestBed.inject(SwUpdateService);
    reloadSpy = vi.spyOn(service, 'reloadApp').mockImplementation(() => {});
  });

  it('AC 2: when versionUpdates emits VERSION_READY, prompts updateAvailable without silent reload', () => {
    expect(service.updateAvailable()).toBe(false);

    const event: VersionReadyEvent = {
      type: 'VERSION_READY',
      currentVersion: { hash: 'hash-v1.2.0' },
      latestVersion: { hash: 'hash-v1.3.0' },
    };

    // Emit VERSION_READY
    versionUpdates$.next(event);

    // Update state should be true and hashes captured
    expect(service.updateAvailable()).toBe(true);
    expect(service.currentVersion()).toBe('hash-v1.2.0');
    expect(service.latestVersion()).toBe('hash-v1.3.0');

    // MUST NOT reload silently
    expect(mockSwUpdate.activateUpdate).not.toHaveBeenCalled();
    expect(reloadSpy).not.toHaveBeenCalled();
  });

  it('AC 2: allows dismissing version update notification', () => {
    versionUpdates$.next({
      type: 'VERSION_READY',
      currentVersion: { hash: 'hash-v1.2.0' },
      latestVersion: { hash: 'hash-v1.3.0' },
    });

    expect(service.updateAvailable()).toBe(true);
    service.dismissUpdateNotification();
    expect(service.updateAvailable()).toBe(false);
  });

  it('AC 2: activateAndReload activates the service worker update then reloads application', async () => {
    versionUpdates$.next({
      type: 'VERSION_READY',
      currentVersion: { hash: 'hash-v1.2.0' },
      latestVersion: { hash: 'hash-v1.3.0' },
    });

    await service.activateAndReload();

    expect(mockSwUpdate.activateUpdate).toHaveBeenCalledTimes(1);
    expect(reloadSpy).toHaveBeenCalledTimes(1);
  });

  it('AC 3: handles unrecoverable state event when cache is corrupted', () => {
    expect(service.isUnrecoverable()).toBe(false);

    const event: UnrecoverableStateEvent = {
      type: 'UNRECOVERABLE_STATE',
      reason: 'Failed to retrieve hashed resource from cache: 404',
    };

    unrecoverable$.next(event);

    expect(service.isUnrecoverable()).toBe(true);
    expect(service.unrecoverableReason()).toBe(
      'Failed to retrieve hashed resource from cache: 404',
    );
  });

  it('checks for update once application becomes stable', async () => {
    expect(mockSwUpdate.checkForUpdate).not.toHaveBeenCalled();

    // Application becomes stable
    isStable$.next(true);

    expect(mockSwUpdate.checkForUpdate).toHaveBeenCalledTimes(1);
  });

  it('gracefully handles update check failure when offline', async () => {
    mockSwUpdate.checkForUpdate = vi.fn().mockRejectedValue(new Error('Network offline'));

    const result = await service.checkForUpdate();
    expect(result).toBe(false);
    expect(service.isChecking()).toBe(false);
  });
});
