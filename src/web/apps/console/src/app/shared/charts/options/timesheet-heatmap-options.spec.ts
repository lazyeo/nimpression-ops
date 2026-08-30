import { describe, it, expect } from 'vitest';
import { buildTimesheetHeatmapOptions, TimesheetHeatmapCell, WEEKDAYS, HOURS } from './timesheet-heatmap-options';
import { LIGHT_THEME, DARK_THEME } from '../theme/chart-theme';

describe('TimesheetHeatmapOptions Pure Function (F14.2)', () => {
  const mockData: TimesheetHeatmapCell[] = [
    { dayOfWeek: 0, hour: 9, totalHours: 18.5, driverCount: 8, isOvertimePeak: false },
    { dayOfWeek: 0, hour: 19, totalHours: 14.0, driverCount: 6, isOvertimePeak: true },
    { dayOfWeek: 4, hour: 21, totalHours: 12.0, driverCount: 5, isOvertimePeak: true },
  ];

  it('should return empty state when data is empty', () => {
    const opt = buildTimesheetHeatmapOptions({ data: [] });
    expect(opt.title).toBeDefined();
    expect((opt.title as { text: string }).text).toContain('暂无工时热力图数据');
  });

  it('should construct 7 weekdays yAxis and 24 hours xAxis', () => {
    const opt = buildTimesheetHeatmapOptions({ data: mockData });
    const x = opt.xAxis as { data: string[] };
    const y = opt.yAxis as { data: string[] };

    expect(x.data).toHaveLength(24);
    expect(x.data[0]).toBe('00:00');
    expect(y.data).toEqual(WEEKDAYS);
  });

  it('should format heatmap series points with totalHours, driverCount, and overtime metadata', () => {
    const opt = buildTimesheetHeatmapOptions({ data: mockData });
    const series = (opt.series as Array<{ data: unknown[] }>)[0];

    expect(series.data).toHaveLength(3);
    expect(series.data[0]).toEqual([9, 0, 18.5, 8, 0]);
    expect(series.data[1]).toEqual([19, 0, 14, 6, 1]);
  });

  it('should format tooltip showing driver count, total hours, and overtime warning', () => {
    const opt = buildTimesheetHeatmapOptions({ data: mockData });
    const tooltip = opt.tooltip as { formatter: (p: unknown) => string };

    const formatted = tooltip.formatter({
      value: [19, 0, 14.0, 6, 1],
    });

    expect(formatted).toContain('周一 19:00 - 20:00');
    expect(formatted).toContain('活跃司机数: <strong>6</strong> 人');
    expect(formatted).toContain('累计总工时: <strong>14</strong> 小时');
    expect(formatted).toContain('加班聚集时段');
  });

  it('should dynamically configure visualMap range and colors based on theme', () => {
    const lightOpt = buildTimesheetHeatmapOptions({ data: mockData, theme: LIGHT_THEME });
    const darkOpt = buildTimesheetHeatmapOptions({ data: mockData, theme: DARK_THEME });

    const lightVm = lightOpt.visualMap as { max: number; inRange: { color: string[] } };
    const darkVm = darkOpt.visualMap as { max: number; inRange: { color: string[] } };

    expect(lightVm.max).toBe(19); // Ceiled from 18.5
    expect(lightVm.inRange.color[0]).toBe('#F1F5F9');
    expect(darkVm.inRange.color[0]).toBe('#1E293B');
  });

  it('should reduce x-axis label interval on mobile', () => {
    const mobileOpt = buildTimesheetHeatmapOptions({ data: mockData, isMobile: true });
    const x = mobileOpt.xAxis as { axisLabel: { interval: number; rotate: number } };

    expect(x.axisLabel.interval).toBe(3);
    expect(x.axisLabel.rotate).toBe(45);
  });
});
