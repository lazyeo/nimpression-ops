import { ChangeDetectionStrategy, Component, input, output, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EChartsOption, ECElementEvent } from 'echarts';
import { BaseChartComponent } from '../../../../shared/charts/base-chart/base-chart.component';
import { ChartThemeConfig } from '../../../../shared/charts/theme/chart-theme';

@Component({
  selector: 'nim-fines-composition-chart',
  standalone: true,
  imports: [CommonModule, BaseChartComponent],
  templateUrl: './fines-composition-chart.component.html',
  styleUrl: './fines-composition-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FinesCompositionChartComponent {
  readonly doughnutOptions = input<EChartsOption | null>(null);
  readonly rankingOptions = input<EChartsOption | null>(null);
  readonly loading = input<boolean>(false);
  readonly error = input<string | null>(null);
  readonly selectedCategory = input<string | null>(null);
  readonly theme = input<ChartThemeConfig>();
  readonly height = input<string>('360px');

  readonly categorySelect = output<string | null>();
  readonly retry = output<void>();

  readonly doughnutChart = viewChild<BaseChartComponent>('doughnutBase');
  readonly rankingChart = viewChild<BaseChartComponent>('rankingBase');

  onDoughnutClick(event: ECElementEvent): void {
    if (event && event.name) {
      if (this.selectedCategory() === event.name) {
        // Toggle off
        this.categorySelect.emit(null);
      } else {
        this.categorySelect.emit(event.name);
      }
    }
  }

  clearFilter(): void {
    this.categorySelect.emit(null);
  }

  exportDoughnutPng(): void {
    this.doughnutChart()?.exportPng('fines-composition-doughnut');
  }

  exportRankingPng(): void {
    this.rankingChart()?.exportPng('fines-ranking-bar');
  }
}
