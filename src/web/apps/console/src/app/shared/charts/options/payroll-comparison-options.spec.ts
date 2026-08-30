import { describe, it, expect } from 'vitest';
import { buildPayrollComparisonOptions, DriverPayrollComparisonItem } from './payroll-comparison-options';
import { LIGHT_THEME, DARK_THEME } from '../theme/chart-theme';

describe('PayrollComparisonOptions Pure Function (F14.6)', () => {
  const mockData: DriverPayrollComparisonItem[] = [
    {
      driverId: 'd1',
      driverName: 'Dave Smith',
      employeeNo: 'EMP-001',
      currentPeriod: {
        regularPay: 2000,
        overtimePay: 450,
        holidayPay: 200,
        totalGross: 2650,
        regularHours: 80,
        overtimeHours: 12,
        holidayHours: 8,
      },
      previousPeriod: {
        regularPay: 1900,
        overtimePay: 300,
        holidayPay: 0,
        totalGross: 2200,
        regularHours: 76,
        overtimeHours: 8,
        holidayHours: 0,
      },
    },
    {
      driverId: 'd2',
      driverName: 'Emma Wilson',
      employeeNo: 'EMP-002',
      currentPeriod: {
        regularPay: 2200,
        overtimePay: 600,
        holidayPay: 0,
        totalGross: 2800,
        regularHours: 80,
        overtimeHours: 15,
        holidayHours: 0,
      },
      previousPeriod: {
        regularPay: 2100,
        overtimePay: 750,
        holidayPay: 150,
        totalGross: 3000,
        regularHours: 80,
        overtimeHours: 20,
        holidayHours: 6,
      },
    },
  ];

  it('should return empty state when data is empty', () => {
    const opt = buildPayrollComparisonOptions({ data: [] });
    expect(opt.title).toBeDefined();
    expect((opt.title as { text: string }).text).toContain('暂无薪资对比数据');
  });

  it('should generate 6 series partitioned into two stack groups (current and previous)', () => {
    const opt = buildPayrollComparisonOptions({ data: mockData });
    const series = opt.series as Array<{ name: string; stack: string; data: number[] }>;

    expect(series).toHaveLength(6);

    // Current period stack
    expect(series[0].name).toBe('本期-普通');
    expect(series[0].stack).toBe('current');
    expect(series[0].data).toEqual([2000, 2200]);

    expect(series[1].name).toBe('本期-加班');
    expect(series[1].stack).toBe('current');
    expect(series[1].data).toEqual([450, 600]);

    expect(series[2].name).toBe('本期-假期');
    expect(series[2].stack).toBe('current');
    expect(series[2].data).toEqual([200, 0]);

    // Previous period stack
    expect(series[3].name).toBe('上期-普通');
    expect(series[3].stack).toBe('previous');
    expect(series[3].data).toEqual([1900, 2100]);

    expect(series[4].name).toBe('上期-加班');
    expect(series[4].stack).toBe('previous');
    expect(series[4].data).toEqual([300, 750]);

    expect(series[5].name).toBe('上期-假期');
    expect(series[5].stack).toBe('previous');
    expect(series[5].data).toEqual([0, 150]);
  });

  it('should format detailed comparison tooltip with hours and percentage change', () => {
    const opt = buildPayrollComparisonOptions({
      data: mockData,
      currentPeriodLabel: '2026-08-11 ~ 08-24',
      previousPeriodLabel: '2026-07-28 ~ 08-10',
    });
    const tooltip = opt.tooltip as { formatter: (p: unknown) => string };

    const formatted = tooltip.formatter([{ dataIndex: 0 }]); // Dave Smith
    expect(formatted).toContain('Dave Smith (EMP-001) — 薪资对比');
    expect(formatted).toContain('2026-08-11 ~ 08-24 (总计: $2,650)');
    expect(formatted).toContain('2026-07-28 ~ 08-10 (总计: $2,200)');
    expect(formatted).toContain('普通工时: $2,000 (80h)');
    expect(formatted).toContain('加班工时: $450 (12h)');
    expect(formatted).toContain('环比变动: +$450 (+20.5%)');
  });
});
