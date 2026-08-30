import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type ChartSkeletonType = 'bar' | 'heatmap' | 'line' | 'doughnut' | 'funnel' | 'grouped-bar';

@Component({
  selector: 'nim-chart-skeleton',
  standalone: true,
  templateUrl: './chart-skeleton.component.html',
  styleUrl: './chart-skeleton.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChartSkeletonComponent {
  readonly type = input<ChartSkeletonType>('bar');
  readonly height = input<string>('320px');
}
