import { EChartsOption } from 'echarts';
import {
  ChartThemeConfig,
  CHART_PALETTE,
  SEMANTIC_COLORS,
  ACCESSIBILITY_MARKERS,
  LIGHT_THEME,
} from '../theme/chart-theme';

export interface VehicleOdometerPoint {
  date: string; // 'YYYY-MM-DD'
  odometerKm: number; // Cumulative km
}

export interface VehicleOdometerSeriesData {
  vehicleId: string;
  rego: string;
  readings: VehicleOdometerPoint[];
  serviceIntervalKm: number;
  lastServiceOdometerKm: number;
  maintenanceThresholdKm: number; // lastServiceOdometerKm + serviceIntervalKm
  isDueForService: boolean;
}

export interface OdometerTrendLabels {
  noData?: string;
  dueForService?: string;
  maintenanceThresholdLine?: string;
  odometerAxis?: string;
  odometerRecordTitle?: string;
  overduePointName?: string;
}

export interface OdometerTrendOptionsParams {
  data: VehicleOdometerSeriesData[];
  theme?: ChartThemeConfig;
  isMobile?: boolean;
  labels?: OdometerTrendLabels;
}

/**
 * Pure function to construct ECharts options for F14.3 Odometer Trend (Multi-series Line + Maintenance Threshold).
 */
export function buildOdometerTrendOptions(params: OdometerTrendOptionsParams): EChartsOption {
  const { data, theme = LIGHT_THEME, isMobile = false, labels = {} } = params;

  const noDataText = labels.noData || 'No odometer trend data available';
  const dueForServiceText = labels.dueForService || 'Due for Service';
  const odometerAxisText = labels.odometerAxis || 'Cumulative Mileage (km)';
  const odometerRecordTitleText = labels.odometerRecordTitle || 'Mileage Record';
  const overduePointNameText = labels.overduePointName || 'Overdue';

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

  // Extract unique sorted list of all dates across all vehicles
  const dateSet = new Set<string>();
  data.forEach((v) => v.readings.forEach((r) => dateSet.add(r.date)));
  const allDates = Array.from(dateSet).sort();

  const seriesList: NonNullable<EChartsOption['series']> = [];

  data.forEach((vehicle, idx) => {
    const color = CHART_PALETTE[idx % CHART_PALETTE.length];
    const markerSymbol = ACCESSIBILITY_MARKERS.shapes[idx % ACCESSIBILITY_MARKERS.shapes.length];
    const lineStyleType =
      ACCESSIBILITY_MARKERS.lineStyles[
        Math.floor(idx / ACCESSIBILITY_MARKERS.shapes.length) %
          ACCESSIBILITY_MARKERS.lineStyles.length
      ];

    // Map readings by date
    const readingMap = new Map<string, number>();
    vehicle.readings.forEach((r) => readingMap.set(r.date, r.odometerKm));

    const linePoints: Array<[string, number]> = [];
    const markPointData: Array<{
      name: string;
      coord: [string, number];
      value: string;
      itemStyle: { color: string };
      label: { color: string; fontSize: number; fontWeight: 'bold' };
    }> = [];

    allDates.forEach((date) => {
      if (readingMap.has(date)) {
        const km = readingMap.get(date)!;
        linePoints.push([date, km]);

        // Check if odometer exceeds maintenance threshold
        if (km >= vehicle.maintenanceThresholdKm) {
          markPointData.push({
            name: overduePointNameText,
            coord: [date, km],
            value: overduePointNameText,
            itemStyle: {
              color: SEMANTIC_COLORS.danger, // #D55E00
            },
            label: {
              color: '#FFFFFF',
              fontSize: 10,
              fontWeight: 'bold',
            },
          });
        }
      }
    });

    // MarkLine for vehicle's maintenance threshold
    const markLineData =
      vehicle.maintenanceThresholdKm > 0
        ? [
            {
              name: `${vehicle.rego}`,
              yAxis: vehicle.maintenanceThresholdKm,
              lineStyle: {
                color: SEMANTIC_COLORS.danger,
                type: 'dashed' as const,
                width: 1.5,
              },
              label: {
                formatter: isMobile
                  ? '{b}'
                  : `${vehicle.rego} (${vehicle.maintenanceThresholdKm} km)`,
                position: 'insideEndTop' as const,
                color: SEMANTIC_COLORS.danger,
                fontSize: 10,
              },
            },
          ]
        : [];

    seriesList.push({
      name: vehicle.rego,
      type: 'line',
      data: linePoints,
      symbol: markerSymbol,
      symbolSize: isMobile ? 6 : 8,
      showSymbol: true,
      smooth: false,
      itemStyle: {
        color,
      },
      lineStyle: {
        color,
        width: 2,
        type: lineStyleType as 'solid' | 'dashed' | 'dotted',
      },
      markPoint: {
        symbol: 'pin',
        symbolSize: isMobile ? 32 : 40,
        data: markPointData,
      },
      markLine:
        idx === 0 || vehicle.isDueForService
          ? {
              symbol: ['none', 'none'],
              data: markLineData,
            }
          : undefined,
    });
  });

  const option: EChartsOption = {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'axis',
      backgroundColor: theme.tooltipBackgroundColor,
      borderColor: theme.tooltipBorderColor,
      textStyle: {
        color: theme.tooltipTextColor,
      },
      formatter: (rawParams: unknown) => {
        const items = rawParams as Array<{
          seriesName: string;
          value: [string, number];
          color: string;
        }>;
        if (!items || items.length === 0) return '';
        const date = items[0].value[0];
        let html = `<div style="font-weight:600;margin-bottom:6px;border-bottom:1px solid ${theme.tooltipBorderColor};padding-bottom:3px;">${date} ${odometerRecordTitleText}</div>`;

        items.forEach((it) => {
          const rego = it.seriesName;
          const km = it.value[1];
          const vehicle = data.find((v) => v.rego === rego);
          const threshold = vehicle?.maintenanceThresholdKm ?? 0;
          const isOver = km >= threshold;

          html += `
            <div style="display:flex;justify-content:space-between;align-items:center;margin:3px 0;gap:16px;">
              <span><span style="display:inline-block;width:10px;height:10px;border-radius:2px;background:${it.color};margin-right:6px;"></span>${rego}</span>
              <span style="font-weight:600;">${km.toLocaleString()} km ${isOver ? `<span style="color:#D55E00;font-size:11px;">(${dueForServiceText})</span>` : ''}</span>
            </div>
          `;
        });
        return html;
      },
    },
    legend: {
      data: data.map((v) => v.rego),
      top: 8,
      type: data.length > 6 ? 'scroll' : 'plain',
      textStyle: {
        color: theme.textColor,
        fontSize: isMobile ? 11 : 12,
      },
    },
    grid: {
      top: isMobile ? 56 : 64,
      left: isMobile ? 36 : 56,
      right: isMobile ? 16 : 40,
      bottom: isMobile ? 60 : 48,
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      data: allDates.map((d) => (isMobile ? d.substring(5) : d)),
      axisLine: {
        lineStyle: {
          color: theme.borderColor,
        },
      },
      axisLabel: {
        color: theme.textSecondaryColor,
        fontSize: isMobile ? 10 : 12,
        interval: isMobile
          ? Math.max(1, Math.floor(allDates.length / 6))
          : Math.max(0, Math.floor(allDates.length / 12)),
        rotate: isMobile ? 45 : 0,
      },
    },
    yAxis: {
      type: 'value',
      name: odometerAxisText,
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
        formatter: (val: number) => (val >= 1000 ? `${(val / 1000).toFixed(0)}k` : `${val}`),
      },
      splitLine: {
        lineStyle: {
          color: theme.splitLineColor,
          type: 'dashed',
        },
      },
    },
    series: seriesList,
  };

  return option;
}
