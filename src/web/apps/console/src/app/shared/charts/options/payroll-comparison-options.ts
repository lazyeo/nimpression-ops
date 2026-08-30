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

export interface PayrollComparisonLabels {
  noData?: string;
  currentPeriod?: string;
  previousPeriod?: string;
  regularPay?: string;
  overtimePay?: string;
  holidayPay?: string;
  currRegular?: string;
  currOvertime?: string;
  currHoliday?: string;
  prevRegular?: string;
  prevOvertime?: string;
  prevHoliday?: string;
  diffChange?: string;
  grossPayAxis?: string;
  comparisonTitleSuffix?: string;
}

export interface PayrollComparisonOptionsParams {
  data: DriverPayrollComparisonItem[];
  currentPeriodLabel?: string;   // e.g. '2026-08-11 ~ 08-24'
  previousPeriodLabel?: string;  // e.g. '2026-07-28 ~ 08-10'
  theme?: ChartThemeConfig;
  isMobile?: boolean;
  labels?: PayrollComparisonLabels;
}

/**
 * Pure function to construct ECharts options for F14.6 Payroll Comparison (Grouped Stacked Bar).
 */
export function buildPayrollComparisonOptions(params: PayrollComparisonOptionsParams): EChartsOption {
  const {
    data,
    currentPeriodLabel,
    previousPeriodLabel,
    theme = LIGHT_THEME,
    isMobile = false,
    labels = {},
  } = params;

  const noDataText = labels.noData || 'No payroll comparison data available';
  const currLabel = currentPeriodLabel || labels.currentPeriod || 'Current Period';
  const prevLabel = previousPeriodLabel || labels.previousPeriod || 'Previous Period';
  const regularPayLabel = labels.regularPay || 'Regular';
  const overtimePayLabel = labels.overtimePay || 'Overtime';
  const holidayPayLabel = labels.holidayPay || 'Holiday';
  const currRegularName = labels.currRegular || 'Current - Regular';
  const currOvertimeName = labels.currOvertime || 'Current - Overtime';
  const currHolidayName = labels.currHoliday || 'Current - Holiday';
  const prevRegularName = labels.prevRegular || 'Previous - Regular';
  const prevOvertimeName = labels.prevOvertime || 'Previous - Overtime';
  const prevHolidayName = labels.prevHoliday || 'Previous - Holiday';
  const diffChangeLabel = labels.diffChange || 'Period Change';
  const grossPayAxisText = labels.grossPayAxis || 'Gross Pay ($)';
  const compSuffix = labels.comparisonTitleSuffix || 'Payroll Comparison';

  if (!data || data.length === 0) {
    return {
      title: {
        text: noDataText,
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
            ${item.driverName} (${item.employeeNo}) — ${compSuffix}
          </div>
          
          <div style="margin-bottom:6px;">
            <div style="font-weight:600;color:${OKABE_ITO_PALETTE.skyBlue};">📅 ${currLabel} ($${currTotal.toLocaleString()})</div>
            <div style="font-size:12px;padding-left:8px;">• ${regularPayLabel}: $${item.currentPeriod.regularPay.toLocaleString()} (${item.currentPeriod.regularHours}h)</div>
            <div style="font-size:12px;padding-left:8px;">• ${overtimePayLabel}: $${item.currentPeriod.overtimePay.toLocaleString()} (${item.currentPeriod.overtimeHours}h)</div>
            <div style="font-size:12px;padding-left:8px;">• ${holidayPayLabel}: $${item.currentPeriod.holidayPay.toLocaleString()} (${item.currentPeriod.holidayHours}h)</div>
          </div>

          <div style="margin-bottom:6px;">
            <div style="font-weight:600;color:${theme.textSecondaryColor};">📅 ${prevLabel} ($${prevTotal.toLocaleString()})</div>
            <div style="font-size:12px;padding-left:8px;">• ${regularPayLabel}: $${item.previousPeriod.regularPay.toLocaleString()} (${item.previousPeriod.regularHours}h)</div>
            <div style="font-size:12px;padding-left:8px;">• ${overtimePayLabel}: $${item.previousPeriod.overtimePay.toLocaleString()} (${item.previousPeriod.overtimeHours}h)</div>
            <div style="font-size:12px;padding-left:8px;">• ${holidayPayLabel}: $${item.previousPeriod.holidayPay.toLocaleString()} (${item.previousPeriod.holidayHours}h)</div>
          </div>

          <div style="font-size:12px;border-top:1px dashed ${theme.tooltipBorderColor};padding-top:4px;color:${diff >= 0 ? OKABE_ITO_PALETTE.bluishGreen : OKABE_ITO_PALETTE.vermilion};font-weight:bold;">
            ${diffChangeLabel}: ${diffSign}$${diff.toLocaleString()} (${diffSign}${diffPercent}%)
          </div>
        `;
      },
    },
    legend: {
      data: [
        currRegularName, currOvertimeName, currHolidayName,
        prevRegularName, prevOvertimeName, prevHolidayName,
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
      name: grossPayAxisText,
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
      // Stack 1: Current Period
      {
        name: currRegularName,
        type: 'bar',
        stack: 'current',
        barGap: '20%',
        data: currRegular,
        itemStyle: {
          color: colors.currRegular,
        },
      },
      {
        name: currOvertimeName,
        type: 'bar',
        stack: 'current',
        data: currOvertime,
        itemStyle: {
          color: colors.currOvertime,
        },
      },
      {
        name: currHolidayName,
        type: 'bar',
        stack: 'current',
        data: currHoliday,
        itemStyle: {
          color: colors.currHoliday,
          borderRadius: [2, 2, 0, 0],
        },
      },

      // Stack 2: Previous Period
      {
        name: prevRegularName,
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
        name: prevOvertimeName,
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
        name: prevHolidayName,
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
