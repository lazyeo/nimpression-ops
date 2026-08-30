import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OfflineQueueService } from './offline-queue.service';
import { I18nPipe } from '../i18n/i18n.pipe';
import { LocaleDatePipe } from '../i18n/locale-date.pipe';

@Component({
  selector: 'nim-offline-status',
  standalone: true,
  imports: [CommonModule, I18nPipe],
  templateUrl: './offline-status.component.html',
  styleUrl: './offline-status.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OfflineStatusComponent {
  readonly offlineQueue = inject(OfflineQueueService);
  readonly showQueueModal = signal<boolean>(false);

  openQueueModal(): void {
    this.showQueueModal.set(true);
  }

  closeQueueModal(): void {
    this.showQueueModal.set(false);
  }

  async retryItem(id: string): Promise<void> {
    await this.offlineQueue.retryItem(id);
  }

  async retryAll(): Promise<void> {
    await this.offlineQueue.retryAll();
  }
}
