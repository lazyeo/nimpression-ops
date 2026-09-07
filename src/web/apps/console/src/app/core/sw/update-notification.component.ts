import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { I18nPipe } from '../i18n/i18n.pipe';
import { IconComponent } from '../../shared/components/icon/icon.component';
import { SwUpdateService } from './sw-update.service';

@Component({
  selector: 'nim-update-notification',
  standalone: true,
  imports: [CommonModule, I18nPipe, IconComponent],
  templateUrl: './update-notification.component.html',
  styleUrl: './update-notification.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UpdateNotificationComponent {
  readonly swUpdate = inject(SwUpdateService);
}
