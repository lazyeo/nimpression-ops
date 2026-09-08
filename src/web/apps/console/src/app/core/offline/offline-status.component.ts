import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OfflineQueueService } from './offline-queue.service';
import { RealtimeService } from '../realtime/realtime.service';
import { I18nPipe } from '../i18n/i18n.pipe';
import { LocaleDatePipe } from '../i18n/locale-date.pipe';
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
  readonly effectiveStatus = computed<'synced' | 'syncing' | 'reconnecting' | 'offline'>(() => {
    // 1. If physical browser network offline or offlineQueue explicitly marked offline
    if (!this.offlineQueue.isOnline() || this.offlineQueue.syncStatus() === 'offline') {
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

    // 4. If offline queue is syncing (normal online replay)
    if (this.offlineQueue.syncStatus() === 'syncing') {
      return 'syncing';
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
    if (status === 'syncing') {
      return 'OFFLINE.STATUS_SYNCING';
    }
    return 'OFFLINE.STATUS_SYNCED';
  });

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
