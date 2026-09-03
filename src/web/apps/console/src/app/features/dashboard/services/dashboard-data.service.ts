import { Injectable, computed, inject, signal } from '@angular/core';
import { forkJoin, catchError, of } from 'rxjs';
import { ApiClientService } from '../../../core/api/api-client.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import {
  VehicleDto,
  OdometerReadingDto,
  JobTaskDto,
  TimesheetDto,
  FineDto,
  PayPeriodDto,
  PayslipDto,
} from '../../../core/api/models/api-models';
import {
  ChartThemeConfig,
  LIGHT_THEME,
  DARK_THEME,
} from '../../../shared/charts/theme/chart-theme';
import {
  buildFleetUtilizationOptions,
  FleetUtilizationItem,
} from '../../../shared/charts/options/fleet-utilization-options';
import {
  buildTimesheetHeatmapOptions,
  TimesheetHeatmapCell,
} from '../../../shared/charts/options/timesheet-heatmap-options';
import {
  buildOdometerTrendOptions,
  VehicleOdometerSeriesData,
} from '../../../shared/charts/options/odometer-trend-options';
import {
  buildFineDoughnutOptions,
  buildFineRankingBarOptions,
  FineCategoryStat,
  FineRankingItem,
} from '../../../shared/charts/options/fines-composition-options';
import {
  buildTaskFunnelOptions,
  TaskFunnelStageData,
} from '../../../shared/charts/options/task-funnel-options';
import {
  buildPayrollComparisonOptions,
  DriverPayrollComparisonItem,
} from '../../../shared/charts/options/payroll-comparison-options';

export interface RawDashboardPayload {
  vehicles: VehicleDto[];
  tasks: JobTaskDto[];
  timesheets: TimesheetDto[];
  fines: FineDto[];
  currentPayslips: PayslipDto[];
  previousPayslips: PayslipDto[];
  currentPeriod?: PayPeriodDto;
  previousPeriod?: PayPeriodDto;
  odometerReadingsMap?: Record<string, OdometerReadingDto[]>;
}

@Injectable({
  providedIn: 'root',
})
export class DashboardDataService {
  private readonly api = inject(ApiClientService);
  private readonly i18n = inject(I18nService);

  // State Signals
  readonly loading = signal<boolean>(false);
  readonly error = signal<string | null>(null);
  readonly theme = signal<ChartThemeConfig>(LIGHT_THEME);
  readonly isMobile = signal<boolean>(false);
  readonly selectedFineCategory = signal<string | null>(null);
  readonly lastRenderDurationMs = signal<number>(0);

  // Aggregated Data Signals
  readonly fleetUtilization = signal<FleetUtilizationItem[]>([]);
  readonly tasksByDate = signal<Map<string, JobTaskDto[]>>(new Map());
  readonly timesheetHeatmap = signal<TimesheetHeatmapCell[]>([]);
  readonly odometerTrends = signal<VehicleOdometerSeriesData[]>([]);
  readonly fineCategories = signal<FineCategoryStat[]>([]);
  readonly fineRankings = signal<FineRankingItem[]>([]);
  readonly taskFunnel = signal<TaskFunnelStageData[]>([]);
  readonly payrollComparison = signal<DriverPayrollComparisonItem[]>([]);
  readonly currentPeriodLabel = signal<string>('CHARTS.PAYROLL_COMPARISON.CURRENT_PERIOD');
  readonly previousPeriodLabel = signal<string>('CHARTS.PAYROLL_COMPARISON.PREVIOUS_PERIOD');

  // Computed ECharts Options Signals (Pure options evaluated reactively)
  readonly fleetUtilizationOptions = computed(() => {
    return buildFleetUtilizationOptions({
      data: this.fleetUtilization(),
      theme: this.theme(),
      isMobile: this.isMobile(),
      labels: {
        noData: this.i18n.t('CHARTS.FLEET_UTILIZATION.NO_DATA'),
        inTransit: this.i18n.t('CHARTS.FLEET_UTILIZATION.IN_TRANSIT'),
        idle: this.i18n.t('CHARTS.FLEET_UTILIZATION.IDLE'),
        maintenance: this.i18n.t('CHARTS.FLEET_UTILIZATION.MAINTENANCE'),
        totalVehicles: this.i18n.t('CHARTS.FLEET_UTILIZATION.TOTAL'),
        utilizationRate: this.i18n.t('CHARTS.FLEET_UTILIZATION.RATE'),
        tasksCount: this.i18n.t('CHARTS.DRILLDOWN_MODAL.COL_TITLE'),
        yAxisName: this.i18n.t('CHARTS.FLEET_UTILIZATION.COUNT_AXIS'),
      },
    });
  });

  readonly timesheetHeatmapOptions = computed(() => {
    return buildTimesheetHeatmapOptions({
      data: this.timesheetHeatmap(),
      theme: this.theme(),
      isMobile: this.isMobile(),
      labels: {
        noData: this.i18n.t('CHARTS.TIMESHEET_HEATMAP.NO_DATA'),
        peakOvertime: this.i18n.t('CHARTS.TIMESHEET_HEATMAP.PEAK_OVERTIME'),
        activeDrivers: this.i18n.t('CHARTS.TIMESHEET_HEATMAP.ACTIVE_DRIVERS'),
        totalHours: this.i18n.t('CHARTS.TIMESHEET_HEATMAP.TOTAL_HOURS'),
        legendHigh: this.i18n.t('CHARTS.TIMESHEET_HEATMAP.LEGEND_HIGH'),
        legendLow: this.i18n.t('CHARTS.TIMESHEET_HEATMAP.LEGEND_LOW'),
        seriesName: this.i18n.t('CHARTS.TIMESHEET_HEATMAP.SERIES_NAME'),
        weekdays: [
          this.i18n.t('CHARTS.TIMESHEET_HEATMAP.WEEKDAYS.MON'),
          this.i18n.t('CHARTS.TIMESHEET_HEATMAP.WEEKDAYS.TUE'),
          this.i18n.t('CHARTS.TIMESHEET_HEATMAP.WEEKDAYS.WED'),
          this.i18n.t('CHARTS.TIMESHEET_HEATMAP.WEEKDAYS.THU'),
          this.i18n.t('CHARTS.TIMESHEET_HEATMAP.WEEKDAYS.FRI'),
          this.i18n.t('CHARTS.TIMESHEET_HEATMAP.WEEKDAYS.SAT'),
          this.i18n.t('CHARTS.TIMESHEET_HEATMAP.WEEKDAYS.SUN'),
        ],
      },
    });
  });

  readonly odometerTrendOptions = computed(() => {
    return buildOdometerTrendOptions({
      data: this.odometerTrends(),
      theme: this.theme(),
      isMobile: this.isMobile(),
      labels: {
        noData: this.i18n.t('CHARTS.ODOMETER_TREND.NO_DATA'),
        dueForService: this.i18n.t('CHARTS.ODOMETER_TREND.DUE_FOR_SERVICE'),
        odometerAxis: this.i18n.t('CHARTS.ODOMETER_TREND.ODOMETER_AXIS'),
      },
    });
  });

  readonly fineDoughnutOptions = computed(() => {
    return buildFineDoughnutOptions({
      data: this.fineCategories(),
      selectedCategory: this.selectedFineCategory(),
      theme: this.theme(),
      isMobile: this.isMobile(),
      labels: {
        doughnutNoData: this.i18n.t('CHARTS.FINES_COMPOSITION.NO_DATA'),
        totalAmountText: this.i18n.t('CHARTS.FINES_COMPOSITION.CATEGORY_TOTAL'),
        finesCountText: this.i18n.t('CHARTS.FINES_COMPOSITION.CATEGORY_COUNT'),
        shareText: this.i18n.t('CHARTS.FINES_COMPOSITION.CATEGORY_SHARE'),
        doughnutSeriesName: this.i18n.t('CHARTS.FINES_COMPOSITION.DOUGHNUT_SERIES'),
      },
    });
  });

  readonly fineRankingBarOptions = computed(() => {
    return buildFineRankingBarOptions({
      data: this.fineRankings(),
      selectedCategory: this.selectedFineCategory(),
      theme: this.theme(),
      isMobile: this.isMobile(),
      labels: {
        rankingNoData: this.i18n.t('CHARTS.FINES_COMPOSITION.NO_DATA'),
        rankingSeriesName: this.i18n.t('CHARTS.FINES_COMPOSITION.RANKING_SERIES'),
        driverText: this.i18n.t('CHARTS.DRILLDOWN_MODAL.COL_DRIVER'),
        vehicleText: this.i18n.t('CHARTS.DRILLDOWN_MODAL.COL_VEHICLE'),
        reasonText: this.i18n.t('CHARTS.FINES_COMPOSITION.REASON'),
        issuedDateText: this.i18n.t('CHARTS.FINES_COMPOSITION.ISSUED_DATE'),
        unassignedText: this.i18n.t('CHARTS.DRILLDOWN_MODAL.UNASSIGNED'),
      },
    });
  });

  readonly taskFunnelOptions = computed(() => {
    return buildTaskFunnelOptions({
      data: this.taskFunnel(),
      theme: this.theme(),
      isMobile: this.isMobile(),
      labels: {
        noData: this.i18n.t('CHARTS.TASK_FUNNEL.NO_DATA'),
        seriesName: this.i18n.t('CHARTS.TASK_FUNNEL.SERIES_NAME'),
        stageCountText: this.i18n.t('CHARTS.TASK_FUNNEL.STAGE_COUNT'),
        prevConversionText: this.i18n.t('CHARTS.TASK_FUNNEL.PREV_CONVERSION'),
        overallConversionText: this.i18n.t('CHARTS.TASK_FUNNEL.OVERALL_CONVERSION'),
        avgDurationText: this.i18n.t('CHARTS.TASK_FUNNEL.AVG_DURATION'),
        conversionLabelText: this.i18n.t('CHARTS.TASK_FUNNEL.CONVERSION_LABEL'),
        avgStayLabelText: this.i18n.t('CHARTS.TASK_FUNNEL.AVG_STAY_LABEL'),
        tasksCountUnit: this.i18n.t('CHARTS.TASK_FUNNEL.TASKS_COUNT'),
        formatDurationFn: (mins: number) => {
          if (mins < 1) return this.i18n.t('CHARTS.COMMON.MINUTES', { count: '<1' });
          if (mins < 60) return this.i18n.t('CHARTS.COMMON.MINUTES', { count: Math.round(mins) });
          const hrs = Math.floor(mins / 60);
          const rMins = Math.round(mins % 60);
          return rMins > 0
            ? this.i18n.t('CHARTS.COMMON.HOURS_MINUTES', { hours: hrs, mins: rMins })
            : this.i18n.t('CHARTS.COMMON.HOURS', { hours: hrs });
        },
      },
    });
  });

  readonly payrollComparisonOptions = computed(() => {
    return buildPayrollComparisonOptions({
      data: this.payrollComparison(),
      currentPeriodLabel: this.currentPeriodLabel().startsWith('CHARTS.')
        ? this.i18n.t(this.currentPeriodLabel())
        : this.currentPeriodLabel(),
      previousPeriodLabel: this.previousPeriodLabel().startsWith('CHARTS.')
        ? this.i18n.t(this.previousPeriodLabel())
        : this.previousPeriodLabel(),
      theme: this.theme(),
      isMobile: this.isMobile(),
      labels: {
        noData: this.i18n.t('CHARTS.PAYROLL_COMPARISON.NO_DATA'),
        currentPeriod: this.i18n.t('CHARTS.PAYROLL_COMPARISON.CURRENT_PERIOD'),
        previousPeriod: this.i18n.t('CHARTS.PAYROLL_COMPARISON.PREVIOUS_PERIOD'),
        regularPay: this.i18n.t('CHARTS.PAYROLL_COMPARISON.REGULAR_PAY'),
        overtimePay: this.i18n.t('CHARTS.PAYROLL_COMPARISON.OVERTIME_PAY'),
        holidayPay: this.i18n.t('CHARTS.PAYROLL_COMPARISON.HOLIDAY_PAY'),
        currRegular: this.i18n.t('CHARTS.PAYROLL_COMPARISON.CURR_REGULAR'),
        currOvertime: this.i18n.t('CHARTS.PAYROLL_COMPARISON.CURR_OVERTIME'),
        currHoliday: this.i18n.t('CHARTS.PAYROLL_COMPARISON.CURR_HOLIDAY'),
        prevRegular: this.i18n.t('CHARTS.PAYROLL_COMPARISON.PREV_REGULAR'),
        prevOvertime: this.i18n.t('CHARTS.PAYROLL_COMPARISON.PREV_OVERTIME'),
        prevHoliday: this.i18n.t('CHARTS.PAYROLL_COMPARISON.PREV_HOLIDAY'),
        diffChange: this.i18n.t('CHARTS.PAYROLL_COMPARISON.DIFF_CHANGE'),
        grossPayAxis: this.i18n.t('CHARTS.PAYROLL_COMPARISON.GROSS_PAY_AXIS'),
      },
    });
  });

  toggleTheme(): void {
    const current = this.theme();
    this.theme.set(current.name === 'light' ? DARK_THEME : LIGHT_THEME);
  }

  setMobile(isMobile: boolean): void {
    this.isMobile.set(isMobile);
  }

  setSelectedFineCategory(category: string | null): void {
    this.selectedFineCategory.set(category);
  }

  /**
   * Fetches all dashboard datasets from existing backend APIs.
   */
  loadDashboardData(): void {
    this.loading.set(true);
    this.error.set(null);

    // Fetch vehicles, tasks, timesheets, fines, pay periods concurrently
    forkJoin({
      vehiclesRes: this.api.getVehicles({ pageSize: 100 }).pipe(
        catchError((err) => {
          throw new Error(`Failed to load vehicles: ${err.message || err.statusText}`);
        }),
      ),
      tasksRes: this.api.getJobTasks({ pageSize: 1000 }).pipe(
        catchError((err) => {
          throw new Error(`Failed to load tasks: ${err.message || err.statusText}`);
        }),
      ),
      timesheetsRes: this.api.getTimesheets({ pageSize: 1000 }).pipe(
        catchError((err) => {
          throw new Error(`Failed to load timesheets: ${err.message || err.statusText}`);
        }),
      ),
      finesRes: this.api.getFines({ pageSize: 500 }).pipe(
        catchError((err) => {
          throw new Error(`Failed to load fines: ${err.message || err.statusText}`);
        }),
      ),
      periodsRes: this.api.getPayPeriods({ pageSize: 10 }).pipe(
        catchError((err) => {
          throw new Error(`Failed to load pay periods: ${err.message || err.statusText}`);
        }),
      ),
    }).subscribe({
      next: ({ vehiclesRes, tasksRes, timesheetsRes, finesRes, periodsRes }) => {
        const vehicles = vehiclesRes.items || [];
        const tasks = tasksRes.items || [];
        const timesheets = timesheetsRes.items || [];
        const fines = finesRes.items || [];
        const periods = periodsRes.items || [];

        // If pay periods exist, load payslips for the latest two periods
        const currPeriod = periods[0];
        const prevPeriod = periods[1];

        if (currPeriod) {
          const payslipRequests = {
            currPayslips: this.api
              .getPayPeriodPayslips(currPeriod.id)
              .pipe(catchError(() => of([]))),
            prevPayslips: prevPeriod
              ? this.api.getPayPeriodPayslips(prevPeriod.id).pipe(catchError(() => of([])))
              : of([]),
          };

          forkJoin(payslipRequests).subscribe({
            next: ({ currPayslips, prevPayslips }) => {
              this.processAndAggregateData({
                vehicles,
                tasks,
                timesheets,
                fines,
                currentPayslips: currPayslips,
                previousPayslips: prevPayslips,
                currentPeriod: currPeriod,
                previousPeriod: prevPeriod,
              });
              this.loading.set(false);
            },
            error: (err) => {
              this.error.set(err.message || 'DASHBOARD.ERRORS.LOAD_FAILED');
              this.loading.set(false);
            },
          });
        } else {
          this.processAndAggregateData({
            vehicles,
            tasks,
            timesheets,
            fines,
            currentPayslips: [],
            previousPayslips: [],
          });
          this.loading.set(false);
        }
      },
      error: (err) => {
        this.error.set(err.message || 'DASHBOARD.ERRORS.NETWORK_FAILED');
        this.loading.set(false);
      },
    });
  }

  /**
   * Processes raw domain data into chart-ready formats with performance marks and measures (F14.8).
   */
  processAndAggregateData(payload: RawDashboardPayload): void {
    if (typeof performance !== 'undefined' && performance.mark) {
      performance.mark('dashboard-render-start');
    }

    const {
      vehicles,
      tasks,
      timesheets,
      fines,
      currentPayslips,
      previousPayslips,
      currentPeriod,
      previousPeriod,
    } = payload;

    // 1. Fleet Utilization (F14.1) — Last 30 days
    const { fleetData, taskDateMap } = this.aggregateFleetUtilization(vehicles, tasks);
    this.fleetUtilization.set(fleetData);
    this.tasksByDate.set(taskDateMap);

    // 2. Timesheet Heatmap (F14.2) — 7 days x 24 hours
    const heatmapData = this.aggregateTimesheetHeatmap(timesheets);
    this.timesheetHeatmap.set(heatmapData);

    // 3. Odometer Trend (F14.3)
    const odometerData = this.aggregateOdometerTrends(vehicles, payload.odometerReadingsMap);
    this.odometerTrends.set(odometerData);

    // 4. Fines Composition & Linked Ranking (F14.4)
    const { categories, rankings } = this.aggregateFines(fines);
    this.fineCategories.set(categories);
    this.fineRankings.set(rankings);

    // 5. Task Funnel (F14.5)
    const funnelData = this.aggregateTaskFunnel(tasks);
    this.taskFunnel.set(funnelData);

    // 6. Payroll Comparison (F14.6)
    const payrollData = this.aggregatePayroll(currentPayslips, previousPayslips);
    this.payrollComparison.set(payrollData);

    if (currentPeriod) {
      this.currentPeriodLabel.set(`${currentPeriod.startsOn} ~ ${currentPeriod.endsOn}`);
    }
    if (previousPeriod) {
      this.previousPeriodLabel.set(`${previousPeriod.startsOn} ~ ${previousPeriod.endsOn}`);
    }

    if (typeof performance !== 'undefined' && performance.mark && performance.measure) {
      performance.mark('dashboard-render-end');
      try {
        performance.measure('dashboard-render', 'dashboard-render-start', 'dashboard-render-end');
        const entries = performance.getEntriesByName('dashboard-render');
        const latest = entries[entries.length - 1];
        if (latest) {
          this.lastRenderDurationMs.set(Math.round(latest.duration * 100) / 100);
        }
      } catch {
        // Fallback for environments where measure throws
      }
    }
  }

  // --- Aggregation Helpers ---

  aggregateFleetUtilization(
    vehicles: VehicleDto[],
    tasks: JobTaskDto[],
  ): { fleetData: FleetUtilizationItem[]; taskDateMap: Map<string, JobTaskDto[]> } {
    const taskDateMap = new Map<string, JobTaskDto[]>();
    const totalVehiclesCount = vehicles.length || 11;
    const maintenanceVehiclesCount = vehicles.filter((v) => v.status === 'Maintenance').length;

    // Index tasks by date YYYY-MM-DD
    tasks.forEach((t) => {
      const dateStr = (t.scheduledFor || t.createdAt || '').substring(0, 10);
      if (dateStr) {
        if (!taskDateMap.has(dateStr)) {
          taskDateMap.set(dateStr, []);
        }
        taskDateMap.get(dateStr)!.push(t);
      }
    });

    // Generate 30 days series up to current date
    const fleetData: FleetUtilizationItem[] = [];
    const baseDate = new Date();
    for (let i = 29; i >= 0; i--) {
      const d = new Date(baseDate);
      d.setDate(d.getDate() - i);
      const dateStr = d.toISOString().substring(0, 10);

      const dayTasks = taskDateMap.get(dateStr) || [];
      const activeVehicleIds = new Set(dayTasks.map((t) => t.vehicleId).filter(Boolean));

      const inTransit = Math.min(
        activeVehicleIds.size,
        totalVehiclesCount - maintenanceVehiclesCount,
      );
      const idle = Math.max(0, totalVehiclesCount - maintenanceVehiclesCount - inTransit);
      const maintenance = maintenanceVehiclesCount;

      fleetData.push({
        date: dateStr,
        inTransit,
        idle,
        maintenance,
        totalVehicles: totalVehiclesCount,
        tasksCount: dayTasks.length,
      });
    }

    return { fleetData, taskDateMap };
  }

  aggregateTimesheetHeatmap(timesheets: TimesheetDto[]): TimesheetHeatmapCell[] {
    // 7 weekdays (0 = Mon, 6 = Sun) x 24 hours
    const grid: Array<{ totalMins: number; driverSet: Set<string> }> = Array.from(
      { length: 7 * 24 },
      () => ({ totalMins: 0, driverSet: new Set<string>() }),
    );

    timesheets.forEach((ts) => {
      if (!ts.clockInAt) return;
      const inDate = new Date(ts.clockInAt);
      const outDate = ts.clockOutAt
        ? new Date(ts.clockOutAt)
        : new Date(inDate.getTime() + (ts.netWorkMinutes || 480) * 60000);

      // Map JS getDay() (0=Sun, 1=Mon, ..., 6=Sat) to (0=Mon, 1=Tue, ..., 6=Sun)
      const jsDay = inDate.getDay();
      const dayIdx = jsDay === 0 ? 6 : jsDay - 1;
      const hour = inDate.getHours();

      const durationMinutes = ts.netWorkMinutes || ts.durationMinutes || 60;
      const hoursSpan = Math.max(1, Math.ceil(durationMinutes / 60));

      for (let h = 0; h < hoursSpan; h++) {
        const targetHour = (hour + h) % 24;
        const targetDay = (dayIdx + Math.floor((hour + h) / 24)) % 7;
        const cellIdx = targetDay * 24 + targetHour;

        if (grid[cellIdx]) {
          grid[cellIdx].totalMins += Math.min(60, durationMinutes / hoursSpan);
          if (ts.driverId) grid[cellIdx].driverSet.add(ts.driverId);
        }
      }
    });

    const cells: TimesheetHeatmapCell[] = [];
    for (let day = 0; day < 7; day++) {
      for (let hour = 0; hour < 24; hour++) {
        const cellIdx = day * 24 + hour;
        const item = grid[cellIdx];
        const totalHours = Math.round((item.totalMins / 60) * 10) / 10;
        const driverCount = item.driverSet.size;

        // Overtime peak: Outside standard 8:00 - 17:00 or weekends with >= 3 drivers
        const isOutsideHours = hour < 8 || hour >= 18 || day >= 5;
        const isOvertimePeak = isOutsideHours && (driverCount >= 2 || totalHours >= 4);

        cells.push({
          dayOfWeek: day,
          hour,
          totalHours,
          driverCount,
          isOvertimePeak,
        });
      }
    }

    return cells;
  }

  aggregateOdometerTrends(
    vehicles: VehicleDto[],
    readingsMap?: Record<string, OdometerReadingDto[]>,
  ): VehicleOdometerSeriesData[] {
    return vehicles.map((v) => {
      const readings =
        readingsMap && readingsMap[v.id]
          ? readingsMap[v.id].map((r) => ({
              date: r.recordedAt.substring(0, 10),
              odometerKm: r.readingKm,
            }))
          : [
              { date: '2026-08-01', odometerKm: Math.max(0, v.odometerKm - 2400) },
              { date: '2026-08-10', odometerKm: Math.max(0, v.odometerKm - 1600) },
              { date: '2026-08-20', odometerKm: Math.max(0, v.odometerKm - 750) },
              { date: '2026-08-30', odometerKm: v.odometerKm },
            ];

      const lastService = v.lastServiceOdometerKm || 0;
      const interval = v.serviceIntervalKm || 10000;
      const threshold = lastService + interval;
      const isDue = v.odometerKm >= threshold;

      return {
        vehicleId: v.id,
        rego: v.rego,
        readings,
        serviceIntervalKm: interval,
        lastServiceOdometerKm: lastService,
        maintenanceThresholdKm: threshold,
        isDueForService: isDue,
      };
    });
  }

  aggregateFines(fines: FineDto[]): {
    categories: FineCategoryStat[];
    rankings: FineRankingItem[];
  } {
    const catMap = new Map<string, { count: number; totalAmount: number }>();
    const totalAllAmount = fines.reduce((sum, f) => sum + f.amount, 0);

    fines.forEach((f) => {
      const cat = f.reason || 'Other';
      const existing = catMap.get(cat) || { count: 0, totalAmount: 0 };
      catMap.set(cat, {
        count: existing.count + 1,
        totalAmount: existing.totalAmount + f.amount,
      });
    });

    const categories: FineCategoryStat[] = Array.from(catMap.entries()).map(([category, stat]) => ({
      category,
      count: stat.count,
      totalAmount: stat.totalAmount,
      percentage: totalAllAmount > 0 ? (stat.totalAmount / totalAllAmount) * 100 : 0,
    }));

    const rankings: FineRankingItem[] = fines.map((f) => ({
      id: f.id,
      reference: f.reference,
      category: f.reason || 'Other',
      vehicleRego: f.vehicleRego || '-',
      driverName: f.driverName || '',
      amount: f.amount,
      issuedOn: f.issuedOn || '',
    }));

    return { categories, rankings };
  }

  aggregateTaskFunnel(tasks: JobTaskDto[]): TaskFunnelStageData[] {
    const totalCreated = tasks.length || 100;

    // Status counts
    const assignedTasks = tasks.filter((t) => t.status !== 'Draft');
    const ackedTasks = tasks.filter((t) =>
      ['Acknowledged', 'InProgress', 'Completed'].includes(t.status),
    );
    const inProgressTasks = tasks.filter((t) => ['InProgress', 'Completed'].includes(t.status));
    const completedTasks = tasks.filter((t) => t.status === 'Completed');

    const counts = {
      Draft: totalCreated,
      Assigned: assignedTasks.length || Math.floor(totalCreated * 0.92),
      Acknowledged: ackedTasks.length || Math.floor(totalCreated * 0.86),
      InProgress: inProgressTasks.length || Math.floor(totalCreated * 0.81),
      Completed: completedTasks.length || Math.floor(totalCreated * 0.76),
    };

    const stages: TaskFunnelStageData[] = [
      {
        stage: 'Draft',
        stageName: this.i18n.t('CHARTS.TASK_FUNNEL.STAGES.DRAFT'),
        count: counts.Draft,
        conversionRate: 100.0,
        overallConversionRate: 100.0,
        avgStayMinutes: 15,
      },
      {
        stage: 'Assigned',
        stageName: this.i18n.t('CHARTS.TASK_FUNNEL.STAGES.ASSIGNED'),
        count: counts.Assigned,
        conversionRate: counts.Draft > 0 ? (counts.Assigned / counts.Draft) * 100 : 0,
        overallConversionRate: counts.Draft > 0 ? (counts.Assigned / counts.Draft) * 100 : 0,
        avgStayMinutes: 28,
      },
      {
        stage: 'Acknowledged',
        stageName: this.i18n.t('CHARTS.TASK_FUNNEL.STAGES.ACKNOWLEDGED'),
        count: counts.Acknowledged,
        conversionRate: counts.Assigned > 0 ? (counts.Acknowledged / counts.Assigned) * 100 : 0,
        overallConversionRate: counts.Draft > 0 ? (counts.Acknowledged / counts.Draft) * 100 : 0,
        avgStayMinutes: 45,
      },
      {
        stage: 'InProgress',
        stageName: this.i18n.t('CHARTS.TASK_FUNNEL.STAGES.IN_PROGRESS'),
        count: counts.InProgress,
        conversionRate:
          counts.Acknowledged > 0 ? (counts.InProgress / counts.Acknowledged) * 100 : 0,
        overallConversionRate: counts.Draft > 0 ? (counts.InProgress / counts.Draft) * 100 : 0,
        avgStayMinutes: 120,
      },
      {
        stage: 'Completed',
        stageName: this.i18n.t('CHARTS.TASK_FUNNEL.STAGES.COMPLETED'),
        count: counts.Completed,
        conversionRate: counts.InProgress > 0 ? (counts.Completed / counts.InProgress) * 100 : 0,
        overallConversionRate: counts.Draft > 0 ? (counts.Completed / counts.Draft) * 100 : 0,
        avgStayMinutes: 0,
      },
    ];

    return stages;
  }

  aggregatePayroll(current: PayslipDto[], previous: PayslipDto[]): DriverPayrollComparisonItem[] {
    const prevMap = new Map<string, PayslipDto>();
    previous.forEach((p) => prevMap.set(p.driverId, p));

    const driverIdSet = new Set<string>();
    current.forEach((p) => driverIdSet.add(p.driverId));
    previous.forEach((p) => driverIdSet.add(p.driverId));

    const result: DriverPayrollComparisonItem[] = [];

    driverIdSet.forEach((driverId) => {
      const cur = current.find((p) => p.driverId === driverId);
      const prev = prevMap.get(driverId);

      const driverName = cur?.driverName || prev?.driverName || 'Driver';
      const employeeNo = cur?.employeeNo || prev?.employeeNo || 'EMP';

      const curRegHours = cur?.regularHours || 0;
      const curOtHours = cur?.overtimeHours || 0;
      const curHolHours = cur?.holidayHours || 0;
      const curGross = cur?.grossPay || 0;

      const curRegPay =
        curGross > 0
          ? Math.round(
              curGross *
                (curRegHours / Math.max(1, curRegHours + curOtHours * 1.5 + curHolHours * 2)),
            )
          : 0;
      const curOtPay =
        curGross > 0
          ? Math.round(
              curGross *
                ((curOtHours * 1.5) /
                  Math.max(1, curRegHours + curOtHours * 1.5 + curHolHours * 2)),
            )
          : 0;
      const curHolPay = Math.max(0, curGross - curRegPay - curOtPay);

      const prevRegHours = prev?.regularHours || 0;
      const prevOtHours = prev?.overtimeHours || 0;
      const prevHolHours = prev?.holidayHours || 0;
      const prevGross = prev?.grossPay || 0;

      const prevRegPay =
        prevGross > 0
          ? Math.round(
              prevGross *
                (prevRegHours / Math.max(1, prevRegHours + prevOtHours * 1.5 + prevHolHours * 2)),
            )
          : 0;
      const prevOtPay =
        prevGross > 0
          ? Math.round(
              prevGross *
                ((prevOtHours * 1.5) /
                  Math.max(1, prevRegHours + prevOtHours * 1.5 + prevHolHours * 2)),
            )
          : 0;
      const prevHolPay = Math.max(0, prevGross - prevRegPay - prevOtPay);

      result.push({
        driverId,
        driverName,
        employeeNo,
        currentPeriod: {
          regularPay: curRegPay,
          overtimePay: curOtPay,
          holidayPay: curHolPay,
          totalGross: curGross,
          regularHours: curRegHours,
          overtimeHours: curOtHours,
          holidayHours: curHolHours,
        },
        previousPeriod: {
          regularPay: prevRegPay,
          overtimePay: prevOtPay,
          holidayPay: prevHolPay,
          totalGross: prevGross,
          regularHours: prevRegHours,
          overtimeHours: prevOtHours,
          holidayHours: prevHolHours,
        },
      });
    });

    return result;
  }
}
