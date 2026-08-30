import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { JobTaskDto } from '../../../../core/api/models/api-models';

@Component({
  selector: 'nim-task-drilldown-dialog',
  standalone: true,
  imports: [CommonModule],
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
      case 'Completed': return 'status-completed';
      case 'InProgress': return 'status-in-progress';
      case 'Acknowledged': return 'status-acked';
      case 'Assigned': return 'status-assigned';
      case 'Draft': return 'status-draft';
      case 'Cancelled': return 'status-cancelled';
      default: return 'status-default';
    }
  }

  getStatusLabel(status: string): string {
    switch (status) {
      case 'Completed': return '已完成';
      case 'InProgress': return '进行中';
      case 'Acknowledged': return '已确认';
      case 'Assigned': return '已指派';
      case 'Draft': return '草稿';
      case 'Cancelled': return '已取消';
      default: return status;
    }
  }
}
