import { describe, it, expect } from 'vitest';
import { buildFleetUtilizationOptions, FleetUtilizationItem } from './fleet-utilization-options';
import { LIGHT_THEME, DARK_THEME, SEMANTIC_COLORS } from '../theme/chart-theme';

describe('FleetUtilizationOptions Pure Function (F14.1)', () => {
  const mockData: FleetUtilizationItem[] = [
    { date: '2026-08-01', inTransit: 7, idle: 3, maintenance: 1, totalVehicles: 11, tasksCount: 14 },
    { date: '2026-08-02', inTransit: 8, idle: 2, maintenance: 1, totalVehicles: 11, tasksCount: 16 },
    { date: '2026-08-03', inTransit: 5, idle: 4, maintenance: 2, totalVehicles: 11, tasksCount: 9 },
  ];

  it('should return empty state title when data is empty', () => {
    const option = buildFleetUtilizationOptions({ data: [] });
    expect(option.title).toBeDefined();
    expect((option.title as { text: string }).text).toContain('暂无车队利用率数据');
  });

  it('should generate 3 stacked bar series with Okabe-Ito semantic colors', () => {
    const option = buildFleetUtilizationOptions({ data: mockData, theme: LIGHT_THEME });
    expect(option.series).toHaveLength(3);
    const series = option.series as Array<{ name: string; type: string; stack: string; data: number[]; itemStyle?: { color: string } }>;
    
    expect(series[0].name).toBe('在途车辆');
    expect(series[0].stack).toBe('vehicles');
    expect(series[0].data).toEqual([7, 8, 5]);
    expect(series[0].itemStyle?.color).toBe(SEMANTIC_COLORS.inTransit);

    expect(series[1].name).toBe('闲置车辆');
    expect(series[1].stack).toBe('vehicles');
    expect(series[1].data).toEqual([3, 2, 4]);
    expect(series[1].itemStyle?.color).toBe(SEMANTIC_COLORS.idle);

    expect(series[2].name).toBe('维修车辆');
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

  it('should adapt x-axis formatting and rotation for mobile', () => {
    const desktopOpt = buildFleetUtilizationOptions({ data: mockData, isMobile: false });
    const mobileOpt = buildFleetUtilizationOptions({ data: mockData, isMobile: true });

    const desktopX = desktopOpt.xAxis as { data: string[]; axisLabel: { rotate: number } };
    const mobileX = mobileOpt.xAxis as { data: string[]; axisLabel: { rotate: number } };

    expect(desktopX.data).toEqual(['2026-08-01', '2026-08-02', '2026-08-03']);
    expect(mobileX.data).toEqual(['08-01', '08-02', '08-03']);
    expect(mobileX.axisLabel.rotate).toBe(45);
    expect(desktopX.axisLabel.rotate).toBe(0);
  });

  it('should include drilldown tooltip formatter', () => {
    const option = buildFleetUtilizationOptions({ data: mockData });
    const tooltip = option.tooltip as { formatter: (params: unknown) => string };
    expect(typeof tooltip.formatter).toBe('function');

    const formatted = tooltip.formatter([
      { name: '2026-08-01', seriesName: '在途车辆', value: 7, color: '#0072B2', dataIndex: 0 },
      { name: '2026-08-01', seriesName: '闲置车辆', value: 3, color: '#56B4E9', dataIndex: 0 },
      { name: '2026-08-01', seriesName: '维修车辆', value: 1, color: '#D55E00', dataIndex: 0 },
    ]);
    expect(formatted).toContain('2026-08-01 车队状态');
    expect(formatted).toContain('当日任务数: <strong>14</strong>');
    expect(formatted).toContain('点击柱子下钻');
  });
});
