import { describe, it, expect } from 'vitest';
import { buildFleetUtilizationOptions, FleetUtilizationItem } from './fleet-utilization-options';
import { LIGHT_THEME, DARK_THEME, SEMANTIC_COLORS } from '../theme/chart-theme';

describe('FleetUtilizationOptions Pure Function (F14.1)', () => {
  const mockData: FleetUtilizationItem[] = [
    {
      date: '2026-08-01',
      inTransit: 7,
      idle: 3,
      maintenance: 1,
      totalVehicles: 11,
      tasksCount: 14,
    },
    {
      date: '2026-08-02',
      inTransit: 8,
      idle: 2,
      maintenance: 1,
      totalVehicles: 11,
      tasksCount: 16,
    },
    { date: '2026-08-03', inTransit: 5, idle: 4, maintenance: 2, totalVehicles: 11, tasksCount: 9 },
  ];

  it('should return empty state title when data is empty', () => {
    const option = buildFleetUtilizationOptions({ data: [] });
    expect(option.title).toBeDefined();
    expect((option.title as { text: string }).text).toContain(
      'No fleet utilization data available',
    );
  });

  it('should generate 3 stacked bar series with Okabe-Ito semantic colors', () => {
    const option = buildFleetUtilizationOptions({ data: mockData, theme: LIGHT_THEME });
    expect(option.series).toHaveLength(3);
    const series = option.series as Array<{
      name: string;
      type: string;
      stack: string;
      data: number[];
      itemStyle?: { color: string };
    }>;

    expect(series[0].name).toBe('In Transit');
    expect(series[0].stack).toBe('vehicles');
    expect(series[0].data).toEqual([7, 8, 5]);
    expect(series[0].itemStyle?.color).toBe(SEMANTIC_COLORS.inTransit);

    expect(series[1].name).toBe('Idle');
    expect(series[1].stack).toBe('vehicles');
    expect(series[1].data).toEqual([3, 2, 4]);
    expect(series[1].itemStyle?.color).toBe(SEMANTIC_COLORS.idle);

    expect(series[2].name).toBe('Under Maintenance');
    expect(series[2].stack).toBe('vehicles');
    expect(series[2].data).toEqual([1, 1, 2]);
    expect(series[2].itemStyle?.color).toBe(SEMANTIC_COLORS.maintenance);
  });

  it('should apply dark theme styling to text, axis, and grid lines', () => {
    const lightOpt = buildFleetUtilizationOptions({ data: mockData, theme: LIGHT_THEME });
    const darkOpt = buildFleetUtilizationOptions({ data: mockData, theme: DARK_THEME });

    const lightLegend = lightOpt.legend as { textStyle: { color: string } };
    const darkLegend = darkOpt.legend as { textStyle: { color: string } };

    expect(lightLegend.textStyle.color).toBe(LIGHT_THEME.textColor);
    expect(darkLegend.textStyle.color).toBe(DARK_THEME.textColor);
  });

  it('should adapt x-axis formatting and rotation for mobile and small datasets', () => {
    const desktopOpt = buildFleetUtilizationOptions({ data: mockData, isMobile: false });
    const mobileOpt = buildFleetUtilizationOptions({ data: mockData, isMobile: true });

    const desktopX = desktopOpt.xAxis as { data: string[]; axisLabel: { rotate: number; interval: number } };
    const mobileX = mobileOpt.xAxis as { data: string[]; axisLabel: { rotate: number; interval: number } };

    expect(desktopX.data).toEqual(['08-01', '08-02', '08-03']);
    expect(mobileX.data).toEqual(['08-01', '08-02', '08-03']);
    expect(desktopX.axisLabel.rotate).toBe(0);
    expect(desktopX.axisLabel.interval).toBe(0);
    expect(mobileX.axisLabel.rotate).toBe(45);
  });

  it('R2: should rotate and format date axis for desktop with 14 items without colliding', () => {
    const data14: FleetUtilizationItem[] = Array.from({ length: 14 }, (_, i) => ({
      date: `2026-08-${(i + 1).toString().padStart(2, '0')}`,
      inTransit: 6,
      idle: 3,
      maintenance: 2,
      totalVehicles: 11,
      tasksCount: 12,
    }));

    const desktopOpt = buildFleetUtilizationOptions({ data: data14, isMobile: false });
    const xAxis = desktopOpt.xAxis as {
      data: string[];
      axisLabel: { rotate: number; interval: number };
      axisTick: { alignWithLabel: boolean };
    };
    const grid = desktopOpt.grid as { bottom: number };

    // Formatted to MM-DD
    expect(xAxis.data[0]).toBe('08-01');
    expect(xAxis.data[13]).toBe('08-14');

    // 14 items on desktop: rotate 45 degrees, interval 0 (every day clearly angled to its column)
    expect(xAxis.axisLabel.rotate).toBe(45);
    expect(xAxis.axisLabel.interval).toBe(0);
    expect(xAxis.axisTick.alignWithLabel).toBe(true);
    expect(grid.bottom).toBeGreaterThanOrEqual(48);
  });

  it('R2: should sample interval on desktop with 30 items to maintain clear spacing', () => {
    const data30: FleetUtilizationItem[] = Array.from({ length: 30 }, (_, i) => ({
      date: `2026-08-${(i + 1).toString().padStart(2, '0')}`,
      inTransit: 6,
      idle: 3,
      maintenance: 2,
      totalVehicles: 11,
      tasksCount: 12,
    }));

    const desktopOpt = buildFleetUtilizationOptions({ data: data30, isMobile: false });
    const xAxis = desktopOpt.xAxis as {
      data: string[];
      axisLabel: { rotate: number; interval: number };
    };

    expect(xAxis.axisLabel.rotate).toBe(45);
    expect(xAxis.axisLabel.interval).toBe(3); // Shows every 4th day (~8 labels total)
  });

  it('should include drilldown tooltip formatter and support custom labels', () => {
    const option = buildFleetUtilizationOptions({
      data: mockData,
      labels: {
        statusTitle: 'Fleet Status',
        tasksCount: 'Tasks',
        drilldownHint: 'Click bar to drill down',
      },
    });
    const tooltip = option.tooltip as { formatter: (params: unknown) => string };
    expect(typeof tooltip.formatter).toBe('function');

    const formatted = tooltip.formatter([
      { name: '2026-08-01', seriesName: 'In Transit', value: 7, color: '#0072B2', dataIndex: 0 },
      { name: '2026-08-01', seriesName: 'Idle', value: 3, color: '#56B4E9', dataIndex: 0 },
      {
        name: '2026-08-01',
        seriesName: 'Under Maintenance',
        value: 1,
        color: '#D55E00',
        dataIndex: 0,
      },
    ]);
    expect(formatted).toContain('2026-08-01 Fleet Status');
    expect(formatted).toContain('Tasks: <strong>14</strong>');
    expect(formatted).toContain('Click bar to drill down');
  });
});
