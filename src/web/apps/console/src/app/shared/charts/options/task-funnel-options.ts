import { EChartsOption } from 'echarts';
import { ChartThemeConfig, OKABE_ITO_PALETTE, LIGHT_THEME } from '../theme/chart-theme';

export interface TaskFunnelStageData {
  stage: 'Draft' | 'Assigned' | 'Acknowledged' | 'InProgress' | 'Completed';
  stageName: string;
  count: number;
  conversionRate: number; // Conversion % from previous stage (100% for first stage)
  overallConversionRate: number; // Conversion % from initial Draft stage
  avgStayMinutes: number; // Average duration spent in this state in minutes
}

export interface TaskFunnelLabels {
  noData?: string;
  seriesName?: string;
  stageCountText?: string;
  prevConversionText?: string;
  overallConversionText?: string;
  avgDurationText?: string;
  conversionLabelText?: string;
  avgStayLabelText?: string;
  tasksCountUnit?: string;
  formatDurationFn?: (minutes: number) => string;
}

export interface TaskFunnelOptionsParams {
  data: TaskFunnelStageData[];
  theme?: ChartThemeConfig;
  isMobile?: boolean;
  labels?: TaskFunnelLabels;
}

export function formatDuration(minutes: number): string {
  if (minutes < 1) return '< 1 min';
  if (minutes < 60) return `${Math.round(minutes)} mins`;
  const hours = Math.floor(minutes / 60);
  const remainingMins = Math.round(minutes % 60);
  return remainingMins > 0 ? `${hours}h ${remainingMins}m` : `${hours}h`;
}

/**
 * Pure function to construct ECharts options for F14.5 Task Funnel Chart.
 */
export function buildTaskFunnelOptions(params: TaskFunnelOptionsParams): EChartsOption {
  const { data, theme = LIGHT_THEME, isMobile = false, labels = {} } = params;

  const noDataText = labels.noData || 'No task funnel data available';
  const seriesNameText = labels.seriesName || 'Task Lifecycle';
  const stageCountLabel = labels.stageCountText || 'Stage Tasks';
  const prevConversionLabel = labels.prevConversionText || 'Conversion from Prev';
  const overallConversionLabel = labels.overallConversionText || 'Overall Conversion';
  const avgDurationLabel = labels.avgDurationText || 'Avg Stay Duration';
  const conversionTag = labels.conversionLabelText || 'Conv';
  const avgStayTag = labels.avgStayLabelText || 'Avg';
  const tasksUnit = labels.tasksCountUnit || 'tasks';
  const durationFormatter = labels.formatDurationFn || formatDuration;

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

  // Okabe-Ito gradient colors from Draft to Completed
  const funnelColors = [
    OKABE_ITO_PALETTE.skyBlue, // Draft: #56B4E9
    OKABE_ITO_PALETTE.blue, // Assigned: #0072B2
    OKABE_ITO_PALETTE.orange, // Acknowledged: #E69F00
    OKABE_ITO_PALETTE.reddishPurple, // InProgress: #CC79A7
    OKABE_ITO_PALETTE.bluishGreen, // Completed: #009E73
  ];

  const seriesData = data.map((item, idx) => {
    const color = funnelColors[idx % funnelColors.length];
    return {
      name: item.stageName,
      value: item.count,
      dataRef: item,
      itemStyle: {
        color,
        borderColor: theme.backgroundColor,
        borderWidth: 2,
        shadowBlur: 4,
        shadowColor: 'rgba(0, 0, 0, 0.1)',
      },
    };
  });

  const option: EChartsOption = {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'item',
      backgroundColor: theme.tooltipBackgroundColor,
      borderColor: theme.tooltipBorderColor,
      textStyle: {
        color: theme.tooltipTextColor,
      },
      formatter: (rawParam: unknown) => {
        const p = rawParam as {
          name: string;
          value: number;
          color: string;
          data: { dataRef: TaskFunnelStageData };
        };
        if (!p || !p.data) return '';
        const stage = p.data.dataRef;
        const durationStr = durationFormatter(stage.avgStayMinutes);

        return `
          <div style="font-weight:600;margin-bottom:6px;border-bottom:1px solid ${theme.tooltipBorderColor};padding-bottom:3px;">
            <span style="display:inline-block;width:10px;height:10px;border-radius:2px;background:${p.color};margin-right:6px;"></span>${stage.stageName} (${stage.stage})
          </div>
          <div style="font-size:12px;margin:2px 0;">${stageCountLabel}: <strong>${stage.count}</strong></div>
          <div style="font-size:12px;margin:2px 0;">${prevConversionLabel}: <strong>${stage.conversionRate.toFixed(1)}%</strong></div>
          <div style="font-size:12px;margin:2px 0;">${overallConversionLabel}: <strong>${stage.overallConversionRate.toFixed(1)}%</strong></div>
          <div style="font-size:12px;margin:2px 0;color:${OKABE_ITO_PALETTE.orange};">${avgDurationLabel}: <strong>${durationStr}</strong></div>
        `;
      },
    },
    legend: {
      data: data.map((d) => d.stageName),
      top: 6,
      textStyle: {
        color: theme.textColor,
        fontSize: isMobile ? 11 : 12,
      },
      icon: 'roundRect',
    },
    series: [
      {
        name: seriesNameText,
        type: 'funnel',
        left: isMobile ? '5%' : '12%',
        top: isMobile ? 48 : 56,
        bottom: 24,
        width: isMobile ? '90%' : '76%',
        min: 0,
        max: Math.max(...data.map((d) => d.count), 1),
        minSize: '18%',
        maxSize: '100%',
        sort: 'none', // Keep logical workflow order (Draft -> Completed)
        gap: 4,
        label: {
          show: true,
          position: isMobile ? 'inside' : 'right',
          formatter: (raw: unknown) => {
            const p = raw as { data: { dataRef: TaskFunnelStageData } };
            const item = p.data.dataRef;
            if (isMobile) {
              return `${item.stageName}: ${item.count} (${item.conversionRate.toFixed(0)}%)`;
            }
            return `{title|${item.stageName}}\n{stat|${conversionTag}: ${item.conversionRate.toFixed(1)}%  |  ${avgStayTag}: ${durationFormatter(item.avgStayMinutes)}}\n{count|${item.count} ${tasksUnit}}`;
          },
          rich: {
            title: {
              color: theme.textColor,
              fontSize: 13,
              fontWeight: 'bold',
              lineHeight: 18,
            },
            stat: {
              color: theme.textSecondaryColor,
              fontSize: 11,
              lineHeight: 16,
            },
            count: {
              color: OKABE_ITO_PALETTE.blue,
              fontSize: 12,
              fontWeight: 600,
              lineHeight: 16,
            },
          },
        },
        labelLine: {
          show: !isMobile,
          length: 16,
          lineStyle: {
            color: theme.borderColor,
            width: 1,
          },
        },
        data: seriesData,
      },
    ],
  };

  return option;
}
