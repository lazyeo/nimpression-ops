import { ApplicationRef, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SwUpdate, UnrecoverableStateEvent, VersionReadyEvent } from '@angular/service-worker';
import { filter, first, interval } from 'rxjs';

/**
 * Service Worker Update Management Service (W26 R1).
 *
 * Polling & Timing Rationale:
 * 1. Startup Check: Waiting for ApplicationRef.isStable ensures the initial critical render,
 *    hydration, and boot requests finish before issuing SW update network checks.
 * 2. Periodic Polling (30 minutes): Logistics drivers and dispatchers run the PWA continuously
 *    over 4-8 hour operational shifts. A 30-minute interval delivers prompt notification of new
 *    releases (preventing stale state machine transitions) while preserving mobile battery life.
 * 3. Visibility Change: Checking for updates when the document becomes visible ensures drivers
 *    returning to the app from navigation or other screens receive newly deployed updates.
 */
@Injectable({
  providedIn: 'root',
})
export class SwUpdateService {
  private readonly swUpdate = inject(SwUpdate, { optional: true });
  private readonly appRef = inject(ApplicationRef);
  private readonly destroyRef = inject(DestroyRef);

  readonly isEnabled = signal<boolean>(this.swUpdate?.isEnabled ?? false);
  readonly updateAvailable = signal<boolean>(false);
  readonly isUnrecoverable = signal<boolean>(false);
  readonly currentVersion = signal<string | null>(null);
  readonly latestVersion = signal<string | null>(null);
  readonly unrecoverableReason = signal<string | null>(null);
  readonly isChecking = signal<boolean>(false);

  // 30 minutes polling interval
  readonly checkIntervalMs = 30 * 60 * 1000;

  constructor() {
    this.initUpdateListeners();
    this.initUpdateScheduler();
  }

  private initUpdateListeners(): void {
    if (!this.swUpdate || !this.swUpdate.isEnabled) {
      return;
    }

    // 1. Listen for VERSION_READY event (Do NOT reload silently; notify user first)
    this.swUpdate.versionUpdates
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        filter((evt): evt is VersionReadyEvent => evt.type === 'VERSION_READY'),
      )
      .subscribe((evt) => {
        this.currentVersion.set(evt.currentVersion?.hash ?? null);
        this.latestVersion.set(evt.latestVersion?.hash ?? null);
        this.updateAvailable.set(true);
      });

    // 2. Listen for UNRECOVERABLE state (corrupted cache / unable to serve assets)
    this.swUpdate.unrecoverable
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((evt: UnrecoverableStateEvent) => {
        this.unrecoverableReason.set(evt.reason ?? 'Unrecoverable cache state');
        this.isUnrecoverable.set(true);
      });
  }

  private initUpdateScheduler(): void {
    if (!this.swUpdate || !this.swUpdate.isEnabled) {
      return;
    }

    // Wait for app stabilization before scheduling background checks
    this.appRef.isStable
      .pipe(
        filter((isStable) => isStable),
        first(),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        void this.checkForUpdate();

        // Periodic background polling
        interval(this.checkIntervalMs)
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe(() => {
            void this.checkForUpdate();
          });
      });

    // Also check on tab/app visibility change to 'visible'
    if (typeof document !== 'undefined' && typeof document.addEventListener === 'function') {
      const visibilityHandler = (): void => {
        if (document.visibilityState === 'visible') {
          void this.checkForUpdate();
        }
      };
      document.addEventListener('visibilitychange', visibilityHandler);
      this.destroyRef.onDestroy(() => {
        document.removeEventListener('visibilitychange', visibilityHandler);
      });
    }
  }

  async checkForUpdate(): Promise<boolean> {
    if (!this.swUpdate || !this.swUpdate.isEnabled) {
      return false;
    }

    try {
      this.isChecking.set(true);
      return await this.swUpdate.checkForUpdate();
    } catch {
      // Gracefully handle network errors or offline state
      return false;
    } finally {
      this.isChecking.set(false);
    }
  }

  async activateUpdate(): Promise<boolean> {
    if (!this.swUpdate || !this.swUpdate.isEnabled) {
      return false;
    }

    try {
      return await this.swUpdate.activateUpdate();
    } catch {
      return false;
    }
  }

  async activateAndReload(): Promise<void> {
    await this.activateUpdate();
    this.reloadApp();
  }

  dismissUpdateNotification(): void {
    this.updateAvailable.set(false);
  }

  reloadApp(): void {
    if (typeof window !== 'undefined' && window.location) {
      window.location.reload();
    }
  }
}
