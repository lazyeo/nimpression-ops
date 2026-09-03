import {
  Component,
  ChangeDetectionStrategy,
  signal,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { AuthService } from '../../../core/auth/auth.service';
import { EmailLogsTabComponent } from './components/email-logs-tab/email-logs-tab.component';
import { EmailTemplatesTabComponent } from './components/email-templates-tab/email-templates-tab.component';
import { PartnerContactsTabComponent } from './components/partner-contacts-tab/partner-contacts-tab.component';
import { ComplianceScanTabComponent } from './components/compliance-scan-tab/compliance-scan-tab.component';

export type NotificationTab = 'logs' | 'templates' | 'partners' | 'compliance';

@Component({
  selector: 'nim-notifications',
  standalone: true,
  imports: [
    CommonModule,
    I18nPipe,
    IconComponent,
    EmailLogsTabComponent,
    EmailTemplatesTabComponent,
    PartnerContactsTabComponent,
    ComplianceScanTabComponent,
  ],
  templateUrl: './notifications.component.html',
  styleUrls: ['./notifications.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotificationsComponent {
  readonly authService = inject(AuthService);
  readonly activeTab = signal<NotificationTab>('logs');

  setActiveTab(tab: NotificationTab): void {
    this.activeTab.set(tab);
  }
}
