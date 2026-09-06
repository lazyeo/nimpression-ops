import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { DashboardDataService, RawDashboardPayload } from './dashboard-data.service';
import {
  VehicleDto,
  JobTaskDto,
  TimesheetDto,
  FineDto,
  PayslipDto,
} from '../../../core/api/models/api-models';
import { DARK_THEME, LIGHT_THEME } from '../../../shared/charts/theme/chart-theme';

describe('DashboardDataService & F14.8 Performance Benchmark', () => {
  let service: DashboardDataService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [DashboardDataService, provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(DashboardDataService);
  });

  it('should initialize with default states and light theme when no dark mode is detected', () => {
    expect(service.loading()).toBe(false);
    expect(service.error()).toBeNull();
    expect(service.theme().name).toBe('light');
  });

  it('R4 Requirement: should automatically initialize with DARK_THEME when prefers-color-scheme is dark without user interaction', () => {
    const originalMatchMedia = window.matchMedia;
    try {
      window.matchMedia = ((query: string) => ({
        matches: query.includes('prefers-color-scheme: dark'),
        media: query,
        onchange: null,
        addListener: () => {},
        removeListener: () => {},
        addEventListener: () => {},
        removeEventListener: () => {},
        dispatchEvent: () => true,
      })) as any;

      // Clean any explicit data-theme attribute
      document.documentElement.removeAttribute('data-theme');

      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [DashboardDataService, provideHttpClient(), provideHttpClientTesting()],
      });

      const darkService = TestBed.inject(DashboardDataService);
      expect(darkService.theme().name).toBe('dark');
      expect(darkService.theme()).toEqual(DARK_THEME);
    } finally {
      window.matchMedia = originalMatchMedia;
      document.documentElement.removeAttribute('data-theme');
    }
  });

  it('R4 Requirement: should initialize with DARK_THEME when data-theme attribute is dark on documentElement', () => {
    try {
      document.documentElement.setAttribute('data-theme', 'dark');

      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [DashboardDataService, provideHttpClient(), provideHttpClientTesting()],
      });

      const explicitDarkService = TestBed.inject(DashboardDataService);
      expect(explicitDarkService.theme().name).toBe('dark');
      expect(explicitDarkService.theme()).toEqual(DARK_THEME);
    } finally {
      document.documentElement.removeAttribute('data-theme');
    }
  });

  it('should toggle between light and dark theme and update DOM data-theme attribute', () => {
    document.documentElement.removeAttribute('data-theme');
    expect(service.theme().name).toBe('light');

    service.toggleTheme();
    expect(service.theme().name).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');

    service.toggleTheme();
    expect(service.theme().name).toBe('light');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');

    document.documentElement.removeAttribute('data-theme');
  });

  describe('F14.8 Performance Benchmark (90 days x 11 vehicles x 10 drivers)', () => {
    it('should aggregate datasets and compute all 6 ECharts options in < 500ms using performance.mark/measure', () => {
      // 1. Synthesize 11 Vehicles
      const vehicles: VehicleDto[] = Array.from({ length: 11 }, (_, i) => ({
        id: `veh-${i + 1}`,
        rego: `NZ-${100 + i}`,
        make: 'Toyota',
        model: 'HiAce',
        year: 2022,
        odometerKm: 45000 + i * 2500,
        serviceIntervalKm: 10000,
        lastServiceOdometerKm: 40000,
        status: i === 10 ? 'Maintenance' : 'Active',
      }));

      // 2. Synthesize 10 Drivers
      const drivers = Array.from({ length: 10 }, (_, i) => ({
        id: `drv-${i + 1}`,
        name: `Driver ${i + 1}`,
        empNo: `EMP-00${i + 1}`,
      }));

      // 3. Synthesize 90 Days of Tasks (~3 tasks per day per active vehicle = ~2700 tasks)
      const tasks: JobTaskDto[] = [];
      const timesheets: TimesheetDto[] = [];
      const fines: FineDto[] = [];

      const baseDate = new Date('2026-08-30T00:00:00Z');

      for (let day = 0; day < 90; day++) {
        const currentDate = new Date(baseDate);
        currentDate.setDate(currentDate.getDate() - day);
        const dateIso = currentDate.toISOString();
        const dateStr = dateIso.substring(0, 10);

        // Daily tasks
        for (let v = 0; v < 10; v++) {
          const driver = drivers[v % drivers.length];
          const vehicle = vehicles[v];

          tasks.push({
            id: `task-${day}-${v}`,
            ref: `TSK-${day}-${v}`,
            title: `Route Delivery #${day}-${v}`,
            status: day === 0 ? 'InProgress' : 'Completed',
            priority: 'Medium',
            scheduledFor: dateIso,
            startedAt: dateIso,
            completedAt: dateIso,
            driverId: driver.id,
            driverName: driver.name,
            vehicleId: vehicle.id,
            vehicleRego: vehicle.rego,
            createdAt: dateIso,
          });
        }

        // Daily timesheets
        for (let d = 0; d < 10; d++) {
          const driver = drivers[d];
          timesheets.push({
            id: `ts-${day}-${d}`,
            driverId: driver.id,
            driverName: driver.name,
            clockInAt: `${dateStr}T08:00:00Z`,
            clockOutAt: `${dateStr}T17:30:00Z`,
            durationMinutes: 570,
            breakMinutes: 30,
            netWorkMinutes: 540,
            status: 'Completed',
            createdAt: dateIso,
          });
        }

        // Periodic fines
        if (day % 3 === 0) {
          const driver = drivers[day % drivers.length];
          const vehicle = vehicles[day % vehicles.length];
          const fineReasons = ['超速违章', '违规停车', '闯红灯', '公交车道占用', '压线行驶'];
          fines.push({
            id: `fine-${day}`,
            driverId: driver.id,
            driverName: driver.name,
            vehicleId: vehicle.id,
            vehicleRego: vehicle.rego,
            issuedOn: dateStr,
            authority: 'NZ Police',
            reference: `NZP-${1000 + day}`,
            amount: 150 + (day % 4) * 80,
            currency: 'NZD',
            reason: fineReasons[day % fineReasons.length],
            status: 'Accepted',
          });
        }
      }

      // 4. Synthesize Payslips for Current & Previous Pay Period (10 drivers each)
      const currentPayslips: PayslipDto[] = drivers.map((d) => ({
        id: `cur-ps-${d.id}`,
        payPeriodId: 'cur-period',
        driverId: d.id,
        driverName: d.name,
        employeeNo: d.empNo,
        startsOn: '2026-08-11',
        endsOn: '2026-08-24',
        regularHours: 80,
        overtimeHours: 12,
        holidayHours: 0,
        grossPay: 2600,
        netPay: 2100,
        status: 'Finalised',
      }));

      const previousPayslips: PayslipDto[] = drivers.map((d) => ({
        id: `prev-ps-${d.id}`,
        payPeriodId: 'prev-period',
        driverId: d.id,
        driverName: d.name,
        employeeNo: d.empNo,
        startsOn: '2026-07-28',
        endsOn: '2026-08-10',
        regularHours: 80,
        overtimeHours: 8,
        holidayHours: 8,
        grossPay: 2500,
        netPay: 2000,
        status: 'Finalised',
      }));

      const payload: RawDashboardPayload = {
        vehicles,
        tasks,
        timesheets,
        fines,
        currentPayslips,
        previousPayslips,
      };

      // Verify dataset size
      expect(vehicles.length).toBe(11);
      expect(tasks.length).toBe(900);
      expect(timesheets.length).toBe(900);
      expect(fines.length).toBe(30);

      // Execute data processing and option generation
      service.processAndAggregateData(payload);

      // Read computed options to trigger reactive evaluation of all 6 charts
      const opt1 = service.fleetUtilizationOptions();
      const opt2 = service.timesheetHeatmapOptions();
      const opt3 = service.odometerTrendOptions();
      const opt4_1 = service.fineDoughnutOptions();
      const opt4_2 = service.fineRankingBarOptions();
      const opt5 = service.taskFunnelOptions();
      const opt6 = service.payrollComparisonOptions();

      // Verify options are correctly built
      expect(opt1.series).toBeDefined();
      expect(opt2.series).toBeDefined();
      expect(opt3.series).toBeDefined();
      expect(opt4_1.series).toBeDefined();
      expect(opt4_2.series).toBeDefined();
      expect(opt5.series).toBeDefined();
      expect(opt6.series).toBeDefined();

      // Verify performance measurement via performance.measure()
      const entries = performance.getEntriesByName('dashboard-render');
      expect(entries.length).toBeGreaterThan(0);
      const latestMeasure = entries[entries.length - 1];

      // Assert duration is strictly < 500ms (Hard requirement F14.8)
      expect(latestMeasure.duration).toBeLessThan(500);
      console.log(
        `[F14.8 Performance Benchmark] Render duration for 90d x 11v x 10d: ${latestMeasure.duration.toFixed(2)}ms (Limit: 500ms)`,
      );
    });
  });
});
