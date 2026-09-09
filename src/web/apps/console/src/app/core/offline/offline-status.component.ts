import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OfflineQueueService } from './offline-queue.service';
import { RealtimeService } from '../realtime/realtime.service';
import { I18nPipe } from '../i18n/i18n.pipe';
import { OfflineQueueItem } from '../models/offline.models';
import { resolveUserFacingErrorCode } from '../errors/user-facing-error';
import { IconComponent } from '../../shared/components/icon/icon.component';

@Component({
  selector: 'nim-offline-status',
  standalone: true,
  imports: [CommonModule, I18nPipe, IconComponent],
  templateUrl: './offline-status.component.html',
  styleUrl: './offline-status.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OfflineStatusComponent {
  readonly offlineQueue = inject(OfflineQueueService);
  readonly realtime = inject(RealtimeService);
  readonly showQueueModal = signal<boolean>(false);

  /**
   * Effective synchronization & connectivity state.
   * Honest visible presentation ensuring disconnected realtime push state is never masked as synced.
   */
  readonly effectiveStatus = computed<
    'synced' | 'syncing' | 'reconnecting' | 'offline' | 'attention' | 'pending'
  >(() => {
    // Queue failures do not imply that the browser connection is offline.
    if (!this.offlineQueue.isOnline()) {
      return 'offline';
    }

    // 2. If SignalR realtime push channel is disconnected
    if (this.realtime.connectionState() === 'disconnected') {
      return 'offline';
    }

    // 3. If either offline queue is reconnecting or realtime is connecting/reconnecting
    if (
      this.offlineQueue.syncStatus() === 'reconnecting' ||
      this.realtime.connectionState() === 'reconnecting' ||
      this.realtime.connectionState() === 'connecting'
    ) {
      return 'reconnecting';
    }

    if (this.offlineQueue.hasFailures()) {
      return 'attention';
    }

    // 4. If offline queue is syncing (normal online replay)
    if (this.offlineQueue.syncStatus() === 'syncing') {
      return 'syncing';
    }

    if (this.offlineQueue.pendingCount() > 0) {
      return 'pending';
    }

    // 5. Fully synced & connected
    return 'synced';
  });

  readonly statusLabelKey = computed<string>(() => {
    const status = this.effectiveStatus();
    if (status === 'offline') {
      if (!this.offlineQueue.isOnline()) {
        return 'OFFLINE.STATUS_OFFLINE';
      }
      return 'OFFLINE.STATUS_DISCONNECTED';
    }
    if (status === 'reconnecting') {
      return 'OFFLINE.STATUS_RECONNECTING';
    }
    if (status === 'attention') {
      return 'OFFLINE.STATUS_ATTENTION';
    }
    if (status === 'pending') {
      return 'OFFLINE.STATUS_PENDING';
    }
    if (status === 'syncing') {
      return 'OFFLINE.STATUS_SYNCING';
    }
    return 'OFFLINE.STATUS_SYNCED';
  });

  operationLabelKey(item: OfflineQueueItem): string {
    if (item.method !== 'POST') return 'OFFLINE.OPERATION_GENERIC';
    if (/^\/api\/dispatch\/tasks\/[^/?#]+\/status$/.test(item.url)) {
      const status =
        item.body && typeof item.body === 'object' && 'status' in item.body
          ? item.body.status
          : undefined;
      switch (status) {
        case 'ACKNOWLEDGED':
          return 'DRIVER.ACCEPT_TASK';
        case 'IN_PROGRESS':
          return 'DRIVER.START_TRIP';
        case 'COMPLETED':
          return 'DRIVER.COMPLETE_TRIP';
      }
    }
    switch (item.url) {
      case '/api/timesheet/clock-in':
        return 'DRIVER.CLOCK_IN';
      case '/api/timesheet/clock-out':
        return 'DRIVER.CLOCK_OUT';
      case '/api/timesheet/start-break':
        return 'DRIVER.START_BREAK';
      case '/api/timesheet/end-break':
        return 'DRIVER.END_BREAK';
      default:
        return 'OFFLINE.OPERATION_GENERIC';
    }
  }

  itemStatusLabelKey(item: OfflineQueueItem): string {
    switch (item.status) {
      case 'failed':
        return 'OFFLINE.STATUS_FAILED';
      case 'syncing':
        return 'OFFLINE.STATUS_SYNCING';
      case 'completed':
        return 'OFFLINE.STATUS_COMPLETED';
      default:
        return 'OFFLINE.STATUS_PENDING';
    }
  }

  queueError(item: OfflineQueueItem) {
    // Stored text/translation keys may be legacy or untrusted; only allowlisted codes are displayed.
    return resolveUserFacingErrorCode(item.errorCode);
  }

  openQueueModal(): void {
    this.showQueueModal.set(true);
  }

  closeQueueModal(): void {
    this.showQueueModal.set(false);
  }

  async retryItem(id: string): Promise<void> {
    await this.offlineQueue.retryItem(id);
  }

  async removeItem(id: string): Promise<void> {
    await this.offlineQueue.removeItem(id);
  }

  async retryAll(): Promise<void> {
    await this.offlineQueue.retryAll();
  }
}
