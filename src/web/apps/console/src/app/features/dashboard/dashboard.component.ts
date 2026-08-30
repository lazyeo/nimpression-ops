import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  OnDestroy,
  inject,
  signal,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { provideHttpClient } from '@angular/common/http';
import { DashboardDataService } from './services/dashboard-data.service';
import { FleetUtilizationChartComponent } from './components/fleet-utilization/fleet-utilization-chart.component';
import { TimesheetHeatmapChartComponent } from './components/timesheet-heatmap/timesheet-heatmap-chart.component';
import { OdometerTrendChartComponent } from './components/odometer-trend/odometer-trend-chart.component';
import { FinesCompositionChartComponent } from './components/fines-composition/fines-composition-chart.component';
import { TaskFunnelChartComponent } from './components/task-funnel/task-funnel-chart.component';
import { PayrollComparisonChartComponent } from './components/payroll-comparison/payroll-comparison-chart.component';
import { TaskDrilldownDialogComponent } from './components/task-drilldown-dialog/task-drilldown-dialog.component';

import { I18nPipe } from '../../core/i18n/i18n.pipe';

@Component({
  selector: 'nim-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    I18nPipe,
    FleetUtilizationChartComponent,
    TimesheetHeatmapChartComponent,
    OdometerTrendChartComponent,
    FinesCompositionChartComponent,
    TaskFunnelChartComponent,
    PayrollComparisonChartComponent,
    TaskDrilldownDialogComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent implements OnInit, OnDestroy {
  readonly dataService = inject(DashboardDataService);

  // Drilldown modal state
  readonly drilldownVisible = signal<boolean>(false);
  readonly drilldownDate = signal<string>('');

  readonly drilldownTasks = computed(() => {
    const date = this.drilldownDate();
    return this.dataService.tasksByDate().get(date) || [];
  });

  // KPI Overview calculations
  readonly activeVehiclesCount = computed(() => {
    const fleet = this.dataService.fleetUtilization();
    if (fleet.length === 0) return 0;
    const latest = fleet[fleet.length - 1];
    return latest?.inTransit ?? 0;
  });

  readonly totalVehiclesCount = computed(() => {
    const fleet = this.dataService.fleetUtilization();
    if (fleet.length === 0) return 11;
    const latest = fleet[fleet.length - 1];
    return latest?.totalVehicles ?? 11;
  });

  readonly maintenanceDueCount = computed(() => {
    const odometerList = this.dataService.odometerTrends();
    return odometerList.filter(v => v.isDueForService).length;
  });

  readonly totalFineAmount = computed(() => {
    const categories = this.dataService.fineCategories();
    return categories.reduce((sum, c) => sum + c.totalAmount, 0);
  });

  readonly overallTaskConversion = computed(() => {
    const stages = this.dataService.taskFunnel();
    if (stages.length === 0) return 0;
    const completed = stages.find(s => s.stage === 'Completed');
    return completed ? completed.overallConversionRate : 0;
  });

  private resizeListener?: () => void;

  ngOnInit(): void {
    this.checkMobile();
    if (typeof window !== 'undefined') {
      this.resizeListener = () => this.checkMobile();
      window.addEventListener('resize', this.resizeListener);
    }
    this.dataService.loadDashboardData();
  }

  ngOnDestroy(): void {
    if (typeof window !== 'undefined' && this.resizeListener) {
      window.removeEventListener('resize', this.resizeListener);
    }
  }

  private checkMobile(): void {
    if (typeof window !== 'undefined') {
      const isMobile = window.innerWidth <= 768;
      this.dataService.setMobile(isMobile);
    }
  }

  onFleetDayDrilldown(dateStr: string): void {
    // Normalise date format YYYY-MM-DD
    let fullDate = dateStr;
    if (dateStr.length === 5) {
      const year = new Date().getFullYear();
      fullDate = `${year}-${dateStr}`;
    }
    this.drilldownDate.set(fullDate);
    this.drilldownVisible.set(true);
  }

  closeDrilldown(): void {
    this.drilldownVisible.set(false);
  }

  toggleTheme(): void {
    this.dataService.toggleTheme();
  }

  reload(): void {
    this.dataService.loadDashboardData();
  }
}
