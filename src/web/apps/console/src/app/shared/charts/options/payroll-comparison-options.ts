import { EChartsOption } from 'echarts';
import { ChartThemeConfig, OKABE_ITO_PALETTE, LIGHT_THEME } from '../theme/chart-theme';

export interface DriverPayrollPeriodBreakdown {
  regularPay: number;
  overtimePay: number;
  holidayPay: number;
  totalGross: number;
  regularHours: number;
  overtimeHours: number;
  holidayHours: number;
}

export interface DriverPayrollComparisonItem {
  driverId: string;
  driverName: string;
  employeeNo: string;
  currentPeriod: DriverPayrollPeriodBreakdown;
  previousPeriod: DriverPayrollPeriodBreakdown;
}

export interface PayrollComparisonOptionsParams {
  data: DriverPayrollComparisonItem[];
  currentPeriodLabel?: string;   // e.g. '2026-08-11 ~ 08-24'
  previousPeriodLabel?: string;  // e.g. '2026-07-28 ~ 08-10'
  theme?: ChartThemeConfig;
  isMobile?: boolean;
}

/**
 * Pure function to construct ECharts options for F14.6 Payroll Comparison (Grouped Stacked Bar).
 */
export function buildPayrollComparisonOptions(params: PayrollComparisonOptionsParams): EChartsOption {
  const {
    data,
    currentPeriodLabel = '本薪期',
    previousPeriodLabel = '上薪期',
    theme = LIGHT_THEME,
    isMobile = false,
  } = params;

  if (!data || data.length === 0) {
    return {
      title: {
        text: '暂无薪资对比数据',
        left: 'center',
        top: 'middle',
        textStyle: {
          color: theme.textMutedColor,
          fontSize: 14,
          fontWeight: 'normal',
        },
      },
    };
  }

  const driverNames = data.map(d => `${d.driverName}`);

  // Series data arrays
  const currRegular = data.map(d => d.currentPeriod.regularPay);
  const currOvertime = data.map(d => d.currentPeriod.overtimePay);
  const currHoliday = data.map(d => d.currentPeriod.holidayPay);

  const prevRegular = data.map(d => d.previousPeriod.regularPay);
  const prevOvertime = data.map(d => d.previousPeriod.overtimePay);
  const prevHoliday = data.map(d => d.previousPeriod.holidayPay);

  // Distinct Okabe-Ito colors:
  // Current: Saturated solid colors
  // Previous: Lighter tints or distinct patterns
  const colors = {
    currRegular: OKABE_ITO_PALETTE.blue,          // #0072B2
    currOvertime: OKABE_ITO_PALETTE.orange,       // #E69F00
    currHoliday: OKABE_ITO_PALETTE.bluishGreen,   // #009E73

    prevRegular: '#6BAED6',                       // Lightened Blue / Sky tint
    prevOvertime: '#FDAE6B',                      // Lightened Orange
    prevHoliday: '#74C476',                       // Lightened Green
  };

  const option: EChartsOption = {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'axis',
      axisPointer: {
        type: 'shadow',
      },
      backgroundColor: theme.tooltipBackgroundColor,
      borderColor: theme.tooltipBorderColor,
      textStyle: {
        color: theme.tooltipTextColor,
      },
      formatter: (rawParams: unknown) => {
        const items = rawParams as Array<{ dataIndex: number }>;
        if (!items || items.length === 0) return '';
        const idx = items[0].dataIndex;
        const item = data[idx];
        if (!item) return '';

        const currTotal = item.currentPeriod.totalGross;
        const prevTotal = item.previousPeriod.totalGross;
        const diff = currTotal - prevTotal;
        const diffPercent = prevTotal > 0 ? ((diff / prevTotal) * 100).toFixed(1) : '+100.0';
        const diffSign = diff >= 0 ? `+` : '';

        return `
          <div style="font-weight:600;margin-bottom:6px;border-bottom:1px solid ${theme.tooltipBorderColor};padding-bottom:3px;">
            ${item.driverName} (${item.employeeNo}) — 薪资对比
          </div>
          
          <div style="margin-bottom:6px;">
            <div style="font-weight:600;color:${OKABE_ITO_PALETTE.skyBlue};">📅 ${currentPeriodLabel} (总计: $${currTotal.toLocaleString()})</div>
            <div style="font-size:12px;padding-left:8px;">• 普通工时: $${item.currentPeriod.regularPay.toLocaleString()} (${item.currentPeriod.regularHours}h)</div>
            <div style="font-size:12px;padding-left:8px;">• 加班工时: $${item.currentPeriod.overtimePay.toLocaleString()} (${item.currentPeriod.overtimeHours}h)</div>
            <div style="font-size:12px;padding-left:8px;">• 假期工时: $${item.currentPeriod.holidayPay.toLocaleString()} (${item.currentPeriod.holidayHours}h)</div>
          </div>

          <div style="margin-bottom:6px;">
            <div style="font-weight:600;color:${theme.textSecondaryColor};">📅 ${previousPeriodLabel} (总计: $${prevTotal.toLocaleString()})</div>
            <div style="font-size:12px;padding-left:8px;">• 普通工时: $${item.previousPeriod.regularPay.toLocaleString()} (${item.previousPeriod.regularHours}h)</div>
            <div style="font-size:12px;padding-left:8px;">• 加班工时: $${item.previousPeriod.overtimePay.toLocaleString()} (${item.previousPeriod.overtimeHours}h)</div>
            <div style="font-size:12px;padding-left:8px;">• 假期工时: $${item.previousPeriod.holidayPay.toLocaleString()} (${item.previousPeriod.holidayHours}h)</div>
          </div>

          <div style="font-size:12px;border-top:1px dashed ${theme.tooltipBorderColor};padding-top:4px;color:${diff >= 0 ? OKABE_ITO_PALETTE.bluishGreen : OKABE_ITO_PALETTE.vermilion};font-weight:bold;">
            环比变动: ${diffSign}$${diff.toLocaleString()} (${diffSign}${diffPercent}%)
          </div>
        `;
      },
    },
    legend: {
      data: [
        `本期-普通`, `本期-加班`, `本期-假期`,
        `上期-普通`, `上期-加班`, `上期-假期`,
      ],
      top: 6,
      textStyle: {
        color: theme.textColor,
        fontSize: isMobile ? 10 : 12,
      },
      icon: 'roundRect',
      itemWidth: 12,
      itemHeight: 8,
    },
    grid: {
      top: isMobile ? 64 : 60,
      left: isMobile ? 36 : 56,
      right: isMobile ? 16 : 36,
      bottom: isMobile ? 70 : 48,
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      data: driverNames,
      axisLine: {
        lineStyle: {
          color: theme.borderColor,
        },
      },
      axisLabel: {
        color: theme.textSecondaryColor,
        fontSize: isMobile ? 10 : 12,
        interval: 0,
        rotate: isMobile ? 45 : (driverNames.length > 8 ? 30 : 0),
      },
    },
    yAxis: {
      type: 'value',
      name: '税前薪资 ($)',
      nameTextStyle: {
        color: theme.textSecondaryColor,
        fontSize: 11,
        align: 'left',
        padding: [0, 0, 4, 0],
      },
      axisLine: {
        show: false,
      },
      axisLabel: {
        color: theme.textSecondaryColor,
        fontSize: 11,
        formatter: (val: number) => `$${val.toLocaleString()}`,
      },
      splitLine: {
        lineStyle: {
          color: theme.splitLineColor,
          type: 'dashed',
        },
      },
    },
    series: [
      // Stack 1: Current Period (本薪期)
      {
        name: `本期-普通`,
        type: 'bar',
        stack: 'current',
        barGap: '20%',
        data: currRegular,
        itemStyle: {
          color: colors.currRegular,
        },
      },
      {
        name: `本期-加班`,
        type: 'bar',
        stack: 'current',
        data: currOvertime,
        itemStyle: {
          color: colors.currOvertime,
        },
      },
      {
        name: `本期-假期`,
        type: 'bar',
        stack: 'current',
        data: currHoliday,
        itemStyle: {
          color: colors.currHoliday,
          borderRadius: [2, 2, 0, 0],
        },
      },

      // Stack 2: Previous Period (上薪期)
      {
        name: `上期-普通`,
        type: 'bar',
        stack: 'previous',
        data: prevRegular,
        itemStyle: {
          color: colors.prevRegular,
          borderType: 'dashed',
          borderColor: '#FFFFFF',
          borderWidth: 0.5,
        },
      },
      {
        name: `上期-加班`,
        type: 'bar',
        stack: 'previous',
        data: prevOvertime,
        itemStyle: {
          color: colors.prevOvertime,
          borderType: 'dashed',
          borderColor: '#FFFFFF',
          borderWidth: 0.5,
        },
      },
      {
        name: `上期-假期`,
        type: 'bar',
        stack: 'previous',
        data: prevHoliday,
        itemStyle: {
          color: colors.prevHoliday,
          borderRadius: [2, 2, 0, 0],
          borderType: 'dashed',
          borderColor: '#FFFFFF',
          borderWidth: 0.5,
        },
      },
    ],
  };

  return option;
}
