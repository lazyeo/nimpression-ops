import { ChangeDetectionStrategy, Component, input, output, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EChartsOption } from 'echarts';
import { BaseChartComponent } from '../../../../shared/charts/base-chart/base-chart.component';
import { ChartThemeConfig } from '../../../../shared/charts/theme/chart-theme';

@Component({
  selector: 'nim-payroll-comparison-chart',
  standalone: true,
  imports: [CommonModule, BaseChartComponent],
  templateUrl: './payroll-comparison-chart.component.html',
  styleUrl: './payroll-comparison-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PayrollComparisonChartComponent {
  readonly options = input<EChartsOption | null>(null);
  readonly loading = input<boolean>(false);
  readonly error = input<string | null>(null);
  readonly currentPeriodLabel = input<string>('本薪期');
  readonly previousPeriodLabel = input<string>('上薪期');
  readonly theme = input<ChartThemeConfig>();
  readonly height = input<string>('360px');

  readonly retry = output<void>();
  readonly baseChart = viewChild(BaseChartComponent);

  exportPng(): void {
    this.baseChart()?.exportPng('payroll-comparison-chart');
  }
}
