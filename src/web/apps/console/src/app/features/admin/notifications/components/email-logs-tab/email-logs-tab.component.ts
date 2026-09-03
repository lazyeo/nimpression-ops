import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  signal,
  inject,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { I18nPipe } from '../../../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../../../core/i18n/locale-date.pipe';
import { IconComponent } from '../../../../../shared/components/icon/icon.component';
import { NotificationService } from '../../services/notification.service';
import { EmailLogDto, EmailLogFilter, PagedResult } from '../../models/notification.models';

@Component({
  selector: 'nim-email-logs-tab',
  standalone: true,
  imports: [CommonModule, FormsModule, I18nPipe, LocaleDatePipe, IconComponent],
  templateUrl: './email-logs-tab.component.html',
  styleUrls: ['./email-logs-tab.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmailLogsTabComponent implements OnInit {
  private readonly notificationService = inject(NotificationService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly logs = signal<EmailLogDto[]>([]);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);
  readonly currentPage = signal(1);
  readonly pageSize = signal(20);

  // Filters
  readonly statusFilter = signal<string>('');
  readonly templateKeyFilter = signal<string>('');
  readonly searchTerm = signal<string>('');

  // Selected Log for error/detail modal
  readonly selectedLog = signal<EmailLogDto | null>(null);
  readonly resendingId = signal<string | null>(null);
  readonly resendSuccessMsg = signal<string | null>(null);

  // Outbox Health Overview Stats
  readonly outboxStats = computed(() => {
    const list = this.logs();
    return {
      total: this.totalCount(),
      sent: list.filter((l) => l.status.toLowerCase() === 'sent').length,
      failed: list.filter((l) => ['failed', 'deadletter'].includes(l.status.toLowerCase())).length,
      pending: list.filter((l) => ['pending', 'sending'].includes(l.status.toLowerCase())).length,
    };
  });

  ngOnInit(): void {
    this.loadLogs();
  }

  loadLogs(): void {
    this.loading.set(true);
    this.error.set(null);

    const filter: EmailLogFilter = {
      status: this.statusFilter() || null,
      templateKey: this.templateKeyFilter() || null,
      searchTerm: this.searchTerm() || null,
      page: this.currentPage(),
      pageSize: this.pageSize(),
    };

    this.notificationService.getEmailLogs(filter).subscribe({
      next: (res: PagedResult<EmailLogDto>) => {
        this.logs.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
        this.totalPages.set(res.totalPages || 1);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.message || 'NOTIFICATIONS.LOGS_LOAD_FAILED');
      },
    });
  }

  onFilterChange(): void {
    this.currentPage.set(1);
    this.loadLogs();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
    this.loadLogs();
  }

  openLogDetail(log: EmailLogDto): void {
    this.selectedLog.set(log);
    this.resendSuccessMsg.set(null);
  }

  closeLogDetail(): void {
    this.selectedLog.set(null);
    this.resendSuccessMsg.set(null);
  }

  resendEmail(id: string, event?: MouseEvent): void {
    if (event) event.stopPropagation();
    this.resendingId.set(id);
    this.resendSuccessMsg.set(null);

    this.notificationService.resendEmail(id).subscribe({
      next: () => {
        this.resendingId.set(null);
        this.resendSuccessMsg.set('NOTIFICATIONS.RESEND_SUCCESS');
        this.loadLogs();
      },
      error: (err) => {
        this.resendingId.set(null);
        this.error.set(err.message || 'NOTIFICATIONS.RESEND_FAILED');
      },
    });
  }
}
