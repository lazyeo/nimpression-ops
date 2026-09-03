import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { JobTaskDto } from '../../../../core/api/models/api-models';
import { I18nPipe } from '../../../../core/i18n/i18n.pipe';
import { IconComponent } from '../../../../shared/components/icon/icon.component';

@Component({
  selector: 'nim-task-drilldown-dialog',
  standalone: true,
  imports: [CommonModule, I18nPipe, IconComponent],
  templateUrl: './task-drilldown-dialog.component.html',
  styleUrl: './task-drilldown-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskDrilldownDialogComponent {
  readonly visible = input<boolean>(false);
  readonly date = input<string>('');
  readonly tasks = input<JobTaskDto[]>([]);
  readonly close = output<void>();

  onClose(): void {
    this.close.emit();
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Completed':
        return 'status-completed';
      case 'InProgress':
        return 'status-in-progress';
      case 'Acknowledged':
        return 'status-acked';
      case 'Assigned':
        return 'status-assigned';
      case 'Draft':
        return 'status-draft';
      case 'Cancelled':
        return 'status-cancelled';
      default:
        return 'status-default';
    }
  }

  getStatusKey(status: string): string {
    switch (status) {
      case 'Completed':
        return 'CHARTS.TASK_FUNNEL.STAGES.COMPLETED';
      case 'InProgress':
        return 'CHARTS.TASK_FUNNEL.STAGES.IN_PROGRESS';
      case 'Acknowledged':
        return 'CHARTS.TASK_FUNNEL.STAGES.ACKNOWLEDGED';
      case 'Assigned':
        return 'CHARTS.TASK_FUNNEL.STAGES.ASSIGNED';
      case 'Draft':
        return 'CHARTS.TASK_FUNNEL.STAGES.DRAFT';
      case 'Cancelled':
        return 'CHARTS.TASK_FUNNEL.STAGES.CANCELLED';
      default:
        return status;
    }
  }
}
