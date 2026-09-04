import {
  Component,
  ChangeDetectionStrategy,
  OnInit,
  signal,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../core/i18n/locale-date.pipe';
import { IconComponent } from '../../../shared/components/icon/icon.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { AuthService } from '../../../core/auth/auth.service';
import { AuditService } from './services/audit.service';
import { AuditEventDto, AuditLogFilter, PagedResult } from './models/audit.models';
import { AuditDiffModalComponent } from './components/audit-diff-modal/audit-diff-modal.component';

@Component({
  selector: 'nim-audit',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    I18nPipe,
    LocaleDatePipe,
    IconComponent,
    StatusBadgeComponent,
    AuditDiffModalComponent,
  ],
  templateUrl: './audit.component.html',
  styleUrls: ['./audit.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditComponent implements OnInit {
  readonly authService = inject(AuthService);
  private readonly auditService = inject(AuditService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly isForbidden = signal(false);
  readonly logs = signal<AuditEventDto[]>([]);
  readonly totalCount = signal(0);
  readonly totalPages = signal(1);
  readonly currentPage = signal(1);
  readonly pageSize = signal(20);

  // Filters
  readonly entityTypeFilter = signal<string>('');
  readonly actionFilter = signal<string>('');
  readonly entityIdFilter = signal<string>('');
  readonly fromDateFilter = signal<string>('');
  readonly toDateFilter = signal<string>('');

  // Export State
  readonly exporting = signal(false);

  // Diff Modal State
  readonly selectedEventForDiff = signal<AuditEventDto | null>(null);

  ngOnInit(): void {
    this.loadAuditLogs();
  }

  loadAuditLogs(): void {
    this.loading.set(true);
    this.error.set(null);
    this.isForbidden.set(false);

    const filter: AuditLogFilter = {
      entityType: this.entityTypeFilter() || null,
      action: this.actionFilter() || null,
      entityId: this.entityIdFilter() || null,
      from: this.fromDateFilter() ? new Date(this.fromDateFilter()).toISOString() : null,
      to: this.toDateFilter() ? new Date(this.toDateFilter()).toISOString() : null,
      page: this.currentPage(),
      pageSize: this.pageSize(),
    };

    this.auditService.getAuditLogs(filter).subscribe({
      next: (res: PagedResult<AuditEventDto>) => {
        this.logs.set(res.items || []);
        this.totalCount.set(res.totalCount || 0);
        this.totalPages.set(res.totalPages || 1);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        if (err.status === 403) {
          this.isForbidden.set(true);
        } else {
          this.error.set(err.message || 'AUDIT.LOAD_FAILED');
        }
      },
    });
  }

  onFilterChange(): void {
    this.currentPage.set(1);
    this.loadAuditLogs();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) return;
    this.currentPage.set(page);
    this.loadAuditLogs();
  }

  openDiffModal(event: AuditEventDto): void {
    this.selectedEventForDiff.set(event);
  }

  closeDiffModal(): void {
    this.selectedEventForDiff.set(null);
  }

  exportCsv(): void {
    this.exporting.set(true);
    const filter: AuditLogFilter = {
      entityType: this.entityTypeFilter() || null,
      action: this.actionFilter() || null,
      entityId: this.entityIdFilter() || null,
      from: this.fromDateFilter() ? new Date(this.fromDateFilter()).toISOString() : null,
      to: this.toDateFilter() ? new Date(this.toDateFilter()).toISOString() : null,
    };

    this.auditService.exportAuditLogsCsv(filter).subscribe({
      next: (blob: Blob) => {
        this.exporting.set(false);
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `audit-logs-${new Date().toISOString().substring(0, 10)}.csv`;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
      },
      error: (err) => {
        this.exporting.set(false);
        this.error.set(err.message || 'AUDIT.EXPORT_FAILED');
      },
    });
  }
}
