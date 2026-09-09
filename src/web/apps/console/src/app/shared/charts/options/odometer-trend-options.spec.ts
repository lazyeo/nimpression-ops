import { describe, it, expect } from 'vitest';
import { buildOdometerTrendOptions, VehicleOdometerSeriesData } from './odometer-trend-options';
import { LIGHT_THEME, SEMANTIC_COLORS, ACCESSIBILITY_MARKERS } from '../theme/chart-theme';

describe('OdometerTrendOptions Pure Function (F14.3)', () => {
  const mockData: VehicleOdometerSeriesData[] = [
    {
      vehicleId: 'v1',
      rego: 'ABC123',
      serviceIntervalKm: 10000,
      lastServiceOdometerKm: 40000,
      maintenanceThresholdKm: 50000,
      isDueForService: true,
      readings: [
        { date: '2026-08-01', odometerKm: 48000 },
        { date: '2026-08-10', odometerKm: 49500 },
        { date: '2026-08-20', odometerKm: 50200 }, // Exceeds threshold 50,000!
      ],
    },
    {
      vehicleId: 'v2',
      rego: 'XYZ789',
      serviceIntervalKm: 15000,
      lastServiceOdometerKm: 20000,
      maintenanceThresholdKm: 35000,
      isDueForService: false,
      readings: [
        { date: '2026-08-01', odometerKm: 25000 },
        { date: '2026-08-10', odometerKm: 27000 },
        { date: '2026-08-20', odometerKm: 29000 },
      ],
    },
  ];

  it('should return empty state when data is empty', () => {
    const opt = buildOdometerTrendOptions({ data: [] });
    expect(opt.title).toBeDefined();
    expect((opt.title as { text: string }).text).toContain('No odometer trend data available');
  });

  it('should create multi-series line chart with accessible symbols and line types', () => {
    const opt = buildOdometerTrendOptions({ data: mockData, theme: LIGHT_THEME });
    const series = opt.series as Array<{
      name: string;
      type: string;
      symbol: string;
      lineStyle: { type: string };
    }>;

    expect(series).toHaveLength(2);
    expect(series[0].name).toBe('ABC123');
    expect(series[1].name).toBe('XYZ789');

    // Verify symbols and line styles use accessible sets
    expect(ACCESSIBILITY_MARKERS.shapes).toContain(series[0].symbol);
    expect(ACCESSIBILITY_MARKERS.shapes).toContain(series[1].symbol);
  });

  it('should mark points exceeding maintenance threshold with red markPoint', () => {
    const opt = buildOdometerTrendOptions({ data: mockData });
    const series = opt.series as Array<{
      name: string;
      markPoint: {
        data: Array<{ name: string; coord: [string, number]; itemStyle: { color: string } }>;
      };
      markLine?: { data: Array<{ name: string; yAxis: number }> };
    }>;

    const v1Series = series[0];
    expect(v1Series.markPoint.data).toHaveLength(1);
    expect(v1Series.markPoint.data[0].name).toBe('Overdue');
    expect(v1Series.markPoint.data[0].coord).toEqual(['2026-08-20', 50200]);
    expect(v1Series.markPoint.data[0].itemStyle.color).toBe(SEMANTIC_COLORS.danger);

    // v2 does not exceed threshold, so markPoint data should be empty
    const v2Series = series[1];
    expect(v2Series.markPoint.data).toHaveLength(0);
  });

  it('should include maintenance threshold markLine for vehicle due for service with insideStartTop label', () => {
    const opt = buildOdometerTrendOptions({ data: mockData });
    const series = opt.series as Array<{
      name: string;
      markLine?: {
        data: Array<{
          name: string;
          yAxis: number;
          label?: { position: string; color: string; fontWeight: string };
        }>;
      };
      emphasis?: { focus: string; lineStyle?: { width: number } };
      lineStyle?: { width: number; opacity: number };
    }>;

    const v1Series = series[0];
    expect(v1Series.markLine).toBeDefined();
    expect(v1Series.markLine?.data[0].yAxis).toBe(50000);
    // Label is placed at the start/left (insideStartTop) to prevent overlapping with the right-end overdue markPoint
    expect(v1Series.markLine?.data[0].label?.position).toBe('insideStartTop');
    expect(v1Series.markLine?.data[0].label?.color).toBe(SEMANTIC_COLORS.danger);
    expect(v1Series.markLine?.data[0].label?.fontWeight).toBe('bold');

    // Due-for-service series has emphasized line width (2.5) and opacity (1.0)
    expect(v1Series.lineStyle?.width).toBe(2.5);
    expect(v1Series.lineStyle?.opacity).toBe(1.0);

    // Non-due series has standard line width (1.5) and muted opacity (0.75)
    const v2Series = series[1];
    expect(v2Series.lineStyle?.width).toBe(1.5);
    expect(v2Series.lineStyle?.opacity).toBe(0.75);

    // All series have emphasis focus on series for interactive highlighting
    expect(v1Series.emphasis?.focus).toBe('series');
    expect(v2Series.emphasis?.focus).toBe('series');
  });

  it('should format tooltip with mileage and maintenance alert tag', () => {
    const opt = buildOdometerTrendOptions({
      data: mockData,
      labels: {
        dueForService: 'Due for Service',
        odometerRecordTitle: 'Mileage Record',
      },
    });
    const tooltip = opt.tooltip as { formatter: (params: unknown) => string };

    const formatted = tooltip.formatter([
      { seriesName: 'ABC123', value: ['2026-08-20', 50200], color: '#0072B2' },
      { seriesName: 'XYZ789', value: ['2026-08-20', 29000], color: '#E69F00' },
    ]);

    expect(formatted).toContain('20/08/2026 Mileage Record');
    expect(formatted).toContain('50,200 km');
    expect(formatted).toContain('(Due for Service)');
  });

  it('R1: should use plain multi-line legend for 11 vehicles and dynamically allocate top headroom', () => {
    const fleet11: VehicleOdometerSeriesData[] = Array.from({ length: 11 }, (_, i) => ({
      vehicleId: `v-${i + 1}`,
      rego: `NZ-10${i}`,
      serviceIntervalKm: 10000,
      lastServiceOdometerKm: 40000,
      maintenanceThresholdKm: 50000,
      isDueForService: i === 0,
      readings: [
        { date: '2026-08-01', odometerKm: 45000 + i * 500 },
        { date: '2026-08-30', odometerKm: 48000 + i * 500 },
      ],
    }));

    const desktopOpt = buildOdometerTrendOptions({ data: fleet11, isMobile: false });
    const mobileOpt = buildOdometerTrendOptions({ data: fleet11, isMobile: true });

    const desktopLegend = desktopOpt.legend as {
      type: string;
      data: string[];
      top: number;
    };
    const desktopGrid = desktopOpt.grid as { top: number };
    const mobileGrid = mobileOpt.grid as { top: number };

    // Legend must be plain (not scroll) to allow wrapping and full visibility of all 11 vehicles
    expect(desktopLegend.type).toBe('plain');
    expect(desktopLegend.data).toHaveLength(11);
    expect(desktopLegend.top).toBe(8);

    // Dynamic grid.top accommodates 2 rows on desktop (>=76px) and 3 rows on mobile (>=86px)
    expect(desktopGrid.top).toBeGreaterThanOrEqual(76);
    expect(mobileGrid.top).toBeGreaterThanOrEqual(86);

    // Verify first vehicle still contains the maintenance threshold markLine
    const series = desktopOpt.series as Array<{
      name: string;
      markLine?: { data: Array<{ name: string; yAxis: number }> };
    }>;
    expect(series[0].markLine).toBeDefined();
    expect(series[0].markLine?.data[0].yAxis).toBe(50000);
  });
});
