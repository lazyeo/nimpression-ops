import { ChangeDetectionStrategy, Component, input, output, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EChartsOption } from 'echarts';
import { BaseChartComponent } from '../../../../shared/charts/base-chart/base-chart.component';
import { ChartThemeConfig } from '../../../../shared/charts/theme/chart-theme';

import { I18nPipe } from '../../../../core/i18n/i18n.pipe';

@Component({
  selector: 'nim-task-funnel-chart',
  standalone: true,
  imports: [CommonModule, BaseChartComponent, I18nPipe],
  templateUrl: './task-funnel-chart.component.html',
  styleUrl: './task-funnel-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskFunnelChartComponent {
  readonly options = input<EChartsOption | null>(null);
  readonly loading = input<boolean>(false);
  readonly error = input<string | null>(null);
  readonly theme = input<ChartThemeConfig>();
  readonly height = input<string>('360px');

  readonly retry = output<void>();
  readonly baseChart = viewChild(BaseChartComponent);

  exportPng(): void {
    this.baseChart()?.exportPng('task-funnel-chart');
  }
}
