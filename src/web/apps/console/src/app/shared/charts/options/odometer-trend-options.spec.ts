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
      markPoint: { data: Array<{ name: string; coord: [string, number]; itemStyle: { color: string } }> };
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

  it('should include maintenance threshold markLine for vehicle due for service', () => {
    const opt = buildOdometerTrendOptions({ data: mockData });
    const series = opt.series as Array<{
      name: string;
      markLine?: { data: Array<{ name: string; yAxis: number }> };
    }>;

    const v1Series = series[0];
    expect(v1Series.markLine).toBeDefined();
    expect(v1Series.markLine?.data[0].yAxis).toBe(50000);
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

    expect(formatted).toContain('2026-08-20 Mileage Record');
    expect(formatted).toContain('50,200 km');
    expect(formatted).toContain('(Due for Service)');
  });
});
