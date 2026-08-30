import { EChartsOption } from 'echarts';
import { ChartThemeConfig, OKABE_ITO_PALETTE, LIGHT_THEME } from '../theme/chart-theme';

export interface TimesheetHeatmapCell {
  dayOfWeek: number;    // 0 = Monday, 6 = Sunday
  hour: number;         // 0 - 23
  totalHours: number;   // Total worked hours in this slot
  driverCount: number;  // Number of active drivers
  isOvertimePeak: boolean; // Flagged if heavily clustered during non-standard hours
}

export interface TimesheetHeatmapLabels {
  noData?: string;
  weekdays?: string[];
  peakOvertime?: string;
  activeDrivers?: string;
  totalHours?: string;
  legendHigh?: string;
  legendLow?: string;
  seriesName?: string;
}

export interface TimesheetHeatmapOptionsParams {
  data: TimesheetHeatmapCell[];
  theme?: ChartThemeConfig;
  isMobile?: boolean;
  labels?: TimesheetHeatmapLabels;
}

export const DEFAULT_WEEKDAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
export const WEEKDAYS = DEFAULT_WEEKDAYS;
export const HOURS = Array.from({ length: 24 }, (_, i) => `${i.toString().padStart(2, '0')}:00`);

/**
 * Pure function to construct ECharts options for F14.2 Timesheet Heatmap (Calendar Week x Hour).
 */
export function buildTimesheetHeatmapOptions(params: TimesheetHeatmapOptionsParams): EChartsOption {
  const { data, theme = LIGHT_THEME, isMobile = false, labels = {} } = params;

  const noDataText = labels.noData || 'No timesheet heatmap data available';
  const weekdaysList = labels.weekdays && labels.weekdays.length === 7 ? labels.weekdays : DEFAULT_WEEKDAYS;
  const activeDriversLabel = labels.activeDrivers || 'Active Drivers';
  const totalHoursLabel = labels.totalHours || 'Total Hours';
  const peakOvertimeLabel = labels.peakOvertime || 'Peak Overtime Cluster';
  const legendHighText = labels.legendHigh || 'High / Overtime';
  const legendLowText = labels.legendLow || 'Low Hours';
  const seriesNameText = labels.seriesName || 'Work Distribution';

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

  // Find maximum totalHours to dynamically scale the visual map
  const maxHours = Math.max(...data.map(d => d.totalHours), 1);

  // ECharts heatmap data format: [hourIndex, dayIndex, totalHours, driverCount, isOvertimePeak]
  const seriesData = data.map(d => [
    d.hour,
    d.dayOfWeek,
    Math.round(d.totalHours * 10) / 10,
    d.driverCount,
    d.isOvertimePeak ? 1 : 0,
  ]);

  const option: EChartsOption = {
    backgroundColor: 'transparent',
    tooltip: {
      position: 'top',
      backgroundColor: theme.tooltipBackgroundColor,
      borderColor: theme.tooltipBorderColor,
      textStyle: {
        color: theme.tooltipTextColor,
      },
      formatter: (rawParams: unknown) => {
        const p = rawParams as {
          value: [number, number, number, number, number];
        };
        if (!p || !p.value) return '';
        const [hour, dayIdx, totalHrs, drivers, isOt] = p.value;
        const weekday = weekdaysList[dayIdx] || `Day ${dayIdx + 1}`;
        const nextHour = (hour + 1) % 24;
        const timeRange = `${hour.toString().padStart(2, '0')}:00 - ${nextHour.toString().padStart(2, '0')}:00`;

        let html = `<div style="font-weight:600;margin-bottom:4px;border-bottom:1px solid ${theme.tooltipBorderColor};padding-bottom:3px;">${weekday} ${timeRange}</div>`;
        html += `<div style="font-size:12px;margin:2px 0;">👥 ${activeDriversLabel}: <strong>${drivers}</strong></div>`;
        html += `<div style="font-size:12px;margin:2px 0;">⏱️ ${totalHoursLabel}: <strong>${totalHrs} h</strong></div>`;
        if (isOt) {
          html += `<div style="font-size:11px;color:${OKABE_ITO_PALETTE.vermilion};font-weight:bold;margin-top:4px;">⚠️ ${peakOvertimeLabel}</div>`;
        }
        return html;
      },
    },
    grid: {
      top: isMobile ? 24 : 32,
      left: isMobile ? 36 : 48,
      right: isMobile ? 16 : 36,
      bottom: isMobile ? 60 : 70,
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      data: HOURS,
      splitArea: {
        show: true,
      },
      axisLine: {
        lineStyle: {
          color: theme.borderColor,
        },
      },
      axisLabel: {
        color: theme.textSecondaryColor,
        fontSize: isMobile ? 9 : 11,
        interval: isMobile ? 3 : 1, // Show every 4th hour on mobile
        rotate: isMobile ? 45 : 0,
      },
    },
    yAxis: {
      type: 'category',
      data: weekdaysList,
      splitArea: {
        show: true,
      },
      axisLine: {
        lineStyle: {
          color: theme.borderColor,
        },
      },
      axisLabel: {
        color: theme.textSecondaryColor,
        fontSize: isMobile ? 11 : 12,
      },
    },
    visualMap: {
      min: 0,
      max: Math.ceil(maxHours),
      calculable: true,
      orient: 'horizontal',
      left: 'center',
      bottom: 0,
      text: [legendHighText, legendLowText],
      textStyle: {
        color: theme.textColor,
        fontSize: isMobile ? 10 : 11,
      },
      inRange: {
        // Okabe-Ito compliant continuous gradient from neutral sky-tint to Okabe-Ito Blue to Vermilion
        color: theme.name === 'dark'
          ? ['#1E293B', '#0284C7', '#0072B2', '#D55E00']
          : ['#F1F5F9', '#BAE6FD', '#0072B2', '#D55E00'],
      },
    },
    series: [
      {
        name: seriesNameText,
        type: 'heatmap',
        data: seriesData,
        label: {
          show: !isMobile && maxHours > 0,
          formatter: (p: any) => {
            const val = Array.isArray(p?.value) ? p.value[2] : p?.value;
            return val > 0 ? `${val}h` : '';
          },
          fontSize: 9,
          color: theme.name === 'dark' ? '#F8FAFC' : '#0F172A',
        },
        emphasis: {
          itemStyle: {
            shadowBlur: 10,
            shadowColor: 'rgba(0, 0, 0, 0.5)',
            borderColor: '#FFFFFF',
            borderWidth: 1.5,
          },
        },
      },
    ],
  };

  return option;
}
