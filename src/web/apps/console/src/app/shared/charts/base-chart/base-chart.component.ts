import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  viewChild,
  effect,
  signal,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgxEchartsDirective, provideEchartsCore } from 'ngx-echarts';
import * as echarts from 'echarts/core';
import { ECharts, ECElementEvent } from 'echarts/core';
import { EChartsOption } from 'echarts';
import { BarChart, LineChart, PieChart, FunnelChart, HeatmapChart } from 'echarts/charts';
import {
  TitleComponent,
  TooltipComponent,
  GridComponent,
  LegendComponent,
  MarkLineComponent,
  MarkPointComponent,
  VisualMapComponent,
  ToolboxComponent,
} from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';

import { ChartSkeletonComponent, ChartSkeletonType } from '../skeletons/chart-skeleton.component';
import { ChartThemeConfig, LIGHT_THEME } from '../theme/chart-theme';

import { I18nPipe } from '../../../core/i18n/i18n.pipe';
import { IconComponent } from '../../components/icon/icon.component';

// Register standard ECharts components
echarts.use([
  BarChart,
  LineChart,
  PieChart,
  FunnelChart,
  HeatmapChart,
  TitleComponent,
  TooltipComponent,
  GridComponent,
  LegendComponent,
  MarkLineComponent,
  MarkPointComponent,
  VisualMapComponent,
  ToolboxComponent,
  CanvasRenderer,
]);

@Component({
  selector: 'nim-base-chart',
  standalone: true,
  imports: [CommonModule, NgxEchartsDirective, ChartSkeletonComponent, I18nPipe, IconComponent],
  providers: [provideEchartsCore({ echarts: () => import('echarts') })],
  templateUrl: './base-chart.component.html',
  styleUrl: './base-chart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BaseChartComponent {
  readonly options = input<EChartsOption | null>(null);
  readonly loading = input<boolean>(false);
  readonly error = input<string | null>(null);
  readonly isEmpty = input<boolean>(false);
  readonly emptyText = input<string>('CHARTS.COMMON.NO_DATA');
  readonly skeletonType = input<ChartSkeletonType>('bar');
  readonly height = input<string>('360px');
  readonly theme = input<ChartThemeConfig>(LIGHT_THEME);
  readonly title = input<string>('');
  readonly exportFileName = input<string>('chart-export');

  readonly chartClick = output<ECElementEvent>();
  readonly chartInit = output<ECharts>();
  readonly retry = output<void>();

  readonly echartsDirective = viewChild(NgxEchartsDirective);
  private chartInstance: ECharts | null = null;

  onChartInit(ec: ECharts): void {
    this.chartInstance = ec;
    this.chartInit.emit(ec);
  }

  onChartClick(event: ECElementEvent): void {
    this.chartClick.emit(event);
  }

  onRetry(): void {
    this.retry.emit();
  }

  /**
   * Exports the current chart instance to PNG.
   */
  exportPng(customFileName?: string): string | null {
    if (!this.chartInstance) return null;
    const fileName = (customFileName || this.exportFileName() || 'chart') + '.png';
    const dataUrl = this.chartInstance.getDataURL({
      type: 'png',
      pixelRatio: 2,
      backgroundColor: this.theme().backgroundColor,
      excludeComponents: ['toolbox'],
    });

    if (typeof document !== 'undefined') {
      const link = document.createElement('a');
      link.href = dataUrl;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    }
    return dataUrl;
  }

  /**
   * Explicit resize method
   */
  resize(): void {
    this.chartInstance?.resize();
  }
}
