import { describe, it, expect } from 'vitest';
import { buildTaskFunnelOptions, formatDuration, TaskFunnelStageData } from './task-funnel-options';
import { LIGHT_THEME, DARK_THEME } from '../theme/chart-theme';

describe('TaskFunnelOptions Pure Function (F14.5)', () => {
  const mockStages: TaskFunnelStageData[] = [
    { stage: 'Draft', stageName: '已创建', count: 100, conversionRate: 100.0, overallConversionRate: 100.0, avgStayMinutes: 12 },
    { stage: 'Assigned', stageName: '已指派', count: 90, conversionRate: 90.0, overallConversionRate: 90.0, avgStayMinutes: 24 },
    { stage: 'Acknowledged', stageName: '已确认', count: 85, conversionRate: 94.4, overallConversionRate: 85.0, avgStayMinutes: 45 },
    { stage: 'InProgress', stageName: '进行中', count: 80, conversionRate: 94.1, overallConversionRate: 80.0, avgStayMinutes: 135 },
    { stage: 'Completed', stageName: '已完成', count: 76, conversionRate: 95.0, overallConversionRate: 76.0, avgStayMinutes: 0 },
  ];

  it('should format durations cleanly into minutes and hours', () => {
    expect(formatDuration(0.5)).toBe('< 1 分钟');
    expect(formatDuration(24)).toBe('24 分钟');
    expect(formatDuration(60)).toBe('1小时');
    expect(formatDuration(135)).toBe('2小时15分');
  });

  it('should return empty state when data is empty', () => {
    const opt = buildTaskFunnelOptions({ data: [] });
    expect(opt.title).toBeDefined();
    expect((opt.title as { text: string }).text).toContain('暂无任务漏斗数据');
  });

  it('should preserve logical workflow order (sort: none)', () => {
    const opt = buildTaskFunnelOptions({ data: mockStages });
    const series = (opt.series as Array<{ sort: string; data: unknown[] }>)[0];

    expect(series.sort).toBe('none');
    expect(series.data).toHaveLength(5);
  });

  it('should format tooltip with conversion rates and average duration', () => {
    const opt = buildTaskFunnelOptions({ data: mockStages });
    const tooltip = opt.tooltip as { formatter: (p: unknown) => string };

    const formatted = tooltip.formatter({
      name: '进行中',
      value: 80,
      color: '#CC79A7',
      data: { dataRef: mockStages[3] },
    });

    expect(formatted).toContain('进行中 (InProgress)');
    expect(formatted).toContain('当前阶段任务量: <strong>80</strong> 单');
    expect(formatted).toContain('上一阶段转化率: <strong>94.1%</strong>');
    expect(formatted).toContain('全链路总转化率: <strong>80.0%</strong>');
    expect(formatted).toContain('平均停留时长: <strong>2小时15分</strong>');
  });

  it('should simplify labels on mobile screens', () => {
    const desktopOpt = buildTaskFunnelOptions({ data: mockStages, isMobile: false });
    const mobileOpt = buildTaskFunnelOptions({ data: mockStages, isMobile: true });

    const desktopSeries = (desktopOpt.series as Array<{ label: { position: string } }>)[0];
    const mobileSeries = (mobileOpt.series as Array<{ label: { position: string } }>)[0];

    expect(desktopSeries.label.position).toBe('right');
    expect(mobileSeries.label.position).toBe('inside');
  });
});
