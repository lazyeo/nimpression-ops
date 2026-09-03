import {
  Component,
  ChangeDetectionStrategy,
  signal,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { I18nPipe } from '../../../../../core/i18n/i18n.pipe';
import { IconComponent } from '../../../../../shared/components/icon/icon.component';
import { NotificationService } from '../../services/notification.service';

@Component({
  selector: 'nim-compliance-scan-tab',
  standalone: true,
  imports: [CommonModule, I18nPipe, IconComponent],
  templateUrl: './compliance-scan-tab.component.html',
  styleUrls: ['./compliance-scan-tab.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ComplianceScanTabComponent {
  private readonly notificationService = inject(NotificationService);

  readonly scanning = signal(false);
  readonly scanSuccess = signal(false);
  readonly scanError = signal<string | null>(null);
  readonly lastScanTime = signal<string | null>(null);

  triggerScan(): void {
    this.scanning.set(true);
    this.scanSuccess.set(false);
    this.scanError.set(null);

    this.notificationService.triggerComplianceScan().subscribe({
      next: () => {
        this.scanning.set(false);
        this.scanSuccess.set(true);
        this.lastScanTime.set(new Date().toISOString());
      },
      error: (err) => {
        this.scanning.set(false);
        this.scanError.set(err.message || 'NOTIFICATIONS.SCAN_FAILED');
      },
    });
  }
}
