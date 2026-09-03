import {
  Component,
  ChangeDetectionStrategy,
  input,
  output,
  computed,
  signal,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { I18nPipe } from '../../../../../core/i18n/i18n.pipe';
import { LocaleDatePipe } from '../../../../../core/i18n/locale-date.pipe';
import { IconComponent } from '../../../../../shared/components/icon/icon.component';
import { AuditService } from '../../services/audit.service';
import { AuditEventDto, DiffFieldItem } from '../../models/audit.models';

@Component({
  selector: 'nim-audit-diff-modal',
  standalone: true,
  imports: [CommonModule, I18nPipe, LocaleDatePipe, IconComponent],
  templateUrl: './audit-diff-modal.component.html',
  styleUrls: ['./audit-diff-modal.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditDiffModalComponent {
  private readonly auditService = inject(AuditService);

  readonly event = input.required<AuditEventDto>();
  readonly closeModal = output<void>();

  readonly activeView = signal<'diff' | 'raw'>('diff');
  readonly showUnchanged = signal(false);

  readonly diffItems = computed<DiffFieldItem[]>(() => {
    const ev = this.event();
    return this.auditService.computeDiff(ev.beforeJson, ev.afterJson);
  });

  readonly visibleDiffItems = computed<DiffFieldItem[]>(() => {
    const list = this.diffItems();
    if (this.showUnchanged()) return list;
    return list.filter((item) => item.changeType !== 'unchanged');
  });

  readonly diffSummary = computed(() => {
    const list = this.diffItems();
    const added = list.filter((i) => i.changeType === 'added').length;
    const removed = list.filter((i) => i.changeType === 'removed').length;
    const modified = list.filter((i) => i.changeType === 'modified').length;
    const unchanged = list.filter((i) => i.changeType === 'unchanged').length;
    return { added, removed, modified, unchanged, total: list.length };
  });

  readonly formattedBeforeJson = computed(() => {
    const raw = this.event().beforeJson;
    if (!raw) return 'null';
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  });

  readonly formattedAfterJson = computed(() => {
    const raw = this.event().afterJson;
    if (!raw) return 'null';
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  });

  toggleUnchanged(): void {
    this.showUnchanged.update((v) => !v);
  }

  setView(view: 'diff' | 'raw'): void {
    this.activeView.set(view);
  }

  close(): void {
    this.closeModal.emit();
  }
}
