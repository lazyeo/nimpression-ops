import { ChangeDetectionStrategy, Component, input, output, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EChartsOption, ECElementEvent } from 'echarts';
import { BaseChartComponent } from '../../../../shared/charts/base-chart/base-chart.component';
import { ChartThemeConfig } from '../../../../shared/charts/theme/chart-theme';

import { I18nPipe } from '../../../../core/i18n/i18n.pipe';

@Component({
  selector: 'nim-fleet-utilization-chart',
  standalone: true,
  imports: [CommonModule, BaseChartComponent, I18nPipe],
  templateUrl: './fleet-utilization-chart.component.html',
  styleUrl: './fleet-utilization-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FleetUtilizationChartComponent {
  readonly options = input<EChartsOption | null>(null);
  readonly loading = input<boolean>(false);
  readonly error = input<string | null>(null);
  readonly theme = input<ChartThemeConfig>();
  readonly height = input<string>('360px');

  readonly dayClick = output<string>();
  readonly retry = output<void>();

  readonly baseChart = viewChild(BaseChartComponent);

  onChartClick(event: ECElementEvent): void {
    if (event && event.name) {
      // Pass the clicked date string to parent component for drilldown
      this.dayClick.emit(event.name);
    }
  }

  exportPng(): void {
    this.baseChart()?.exportPng('fleet-utilization-chart');
  }
}
