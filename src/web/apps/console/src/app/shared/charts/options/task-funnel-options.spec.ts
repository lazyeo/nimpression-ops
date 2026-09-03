import { describe, it, expect } from 'vitest';
import { buildTaskFunnelOptions, formatDuration, TaskFunnelStageData } from './task-funnel-options';
import { LIGHT_THEME, DARK_THEME } from '../theme/chart-theme';

describe('TaskFunnelOptions Pure Function (F14.5)', () => {
  const mockStages: TaskFunnelStageData[] = [
    {
      stage: 'Draft',
      stageName: 'Draft',
      count: 100,
      conversionRate: 100.0,
      overallConversionRate: 100.0,
      avgStayMinutes: 12,
    },
    {
      stage: 'Assigned',
      stageName: 'Assigned',
      count: 90,
      conversionRate: 90.0,
      overallConversionRate: 90.0,
      avgStayMinutes: 24,
    },
    {
      stage: 'Acknowledged',
      stageName: 'Acknowledged',
      count: 85,
      conversionRate: 94.4,
      overallConversionRate: 85.0,
      avgStayMinutes: 45,
    },
    {
      stage: 'InProgress',
      stageName: 'In Progress',
      count: 80,
      conversionRate: 94.1,
      overallConversionRate: 80.0,
      avgStayMinutes: 135,
    },
    {
      stage: 'Completed',
      stageName: 'Completed',
      count: 76,
      conversionRate: 95.0,
      overallConversionRate: 76.0,
      avgStayMinutes: 0,
    },
  ];

  it('should format durations cleanly into minutes and hours', () => {
    expect(formatDuration(0.5)).toBe('< 1 min');
    expect(formatDuration(24)).toBe('24 mins');
    expect(formatDuration(60)).toBe('1h');
    expect(formatDuration(135)).toBe('2h 15m');
  });

  it('should return empty state when data is empty', () => {
    const opt = buildTaskFunnelOptions({ data: [] });
    expect(opt.title).toBeDefined();
    expect((opt.title as { text: string }).text).toContain('No task funnel data available');
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
      name: 'In Progress',
      value: 80,
      color: '#CC79A7',
      data: { dataRef: mockStages[3] },
    });

    expect(formatted).toContain('In Progress (InProgress)');
    expect(formatted).toContain('Stage Tasks: <strong>80</strong>');
    expect(formatted).toContain('Conversion from Prev: <strong>94.1%</strong>');
    expect(formatted).toContain('Overall Conversion: <strong>80.0%</strong>');
    expect(formatted).toContain('Avg Stay Duration: <strong>2h 15m</strong>');
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
