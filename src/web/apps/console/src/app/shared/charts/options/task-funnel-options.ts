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
  stageCountFormatter?: (count: number) => string;
  prevConversionFormatter?: (rate: number) => string;
  overallConversionFormatter?: (rate: number) => string;
  avgDurationFormatter?: (duration: string) => string;
  conversionLabelFormatter?: (rate: number) => string;
  avgStayLabelFormatter?: (duration: string) => string;
  tasksCountFormatter?: (count: number) => string;
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

  const formatStageCount = (count: number) => {
    if (labels.stageCountFormatter) return labels.stageCountFormatter(count);
    if (stageCountLabel.includes('{count}')) return stageCountLabel.replace('{count}', String(count));
    return `${stageCountLabel}: <strong>${count}</strong>`;
  };

  const formatPrevConversion = (rate: number) => {
    if (labels.prevConversionFormatter) return labels.prevConversionFormatter(rate);
    if (prevConversionLabel.includes('{rate}')) return prevConversionLabel.replace('{rate}', rate.toFixed(1));
    return `${prevConversionLabel}: <strong>${rate.toFixed(1)}%</strong>`;
  };

  const formatOverallConversion = (rate: number) => {
    if (labels.overallConversionFormatter) return labels.overallConversionFormatter(rate);
    if (overallConversionLabel.includes('{rate}')) return overallConversionLabel.replace('{rate}', rate.toFixed(1));
    return `${overallConversionLabel}: <strong>${rate.toFixed(1)}%</strong>`;
  };

  const formatAvgDuration = (durationStr: string) => {
    if (labels.avgDurationFormatter) return labels.avgDurationFormatter(durationStr);
    if (avgDurationLabel.includes('{duration}')) return avgDurationLabel.replace('{duration}', durationStr);
    return `${avgDurationLabel}: <strong>${durationStr}</strong>`;
  };

  const formatConversionTag = (rate: number) => {
    if (labels.conversionLabelFormatter) return labels.conversionLabelFormatter(rate);
    if (conversionTag.includes('{rate}')) return conversionTag.replace('{rate}', rate.toFixed(1));
    return `${conversionTag}: ${rate.toFixed(1)}%`;
  };

  const formatAvgStayTag = (durationStr: string) => {
    if (labels.avgStayLabelFormatter) return labels.avgStayLabelFormatter(durationStr);
    if (avgStayTag.includes('{duration}')) return avgStayTag.replace('{duration}', durationStr);
    return `${avgStayTag}: ${durationStr}`;
  };

  const formatTasksCount = (count: number) => {
    if (labels.tasksCountFormatter) return labels.tasksCountFormatter(count);
    if (tasksUnit.includes('{count}')) return tasksUnit.replace('{count}', String(count));
    return `${count} ${tasksUnit}`;
  };

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

        const countLine = formatStageCount(stage.count);
        const prevLine = formatPrevConversion(stage.conversionRate);
        const overallLine = formatOverallConversion(stage.overallConversionRate);
        const durationLine = formatAvgDuration(durationStr);

        return `
          <div style="font-weight:600;margin-bottom:6px;border-bottom:1px solid ${theme.tooltipBorderColor};padding-bottom:3px;">
            <span style="display:inline-block;width:10px;height:10px;border-radius:2px;background:${p.color};margin-right:6px;"></span>${stage.stageName} (${stage.stage})
          </div>
          <div style="font-size:12px;margin:2px 0;">${countLine}</div>
          <div style="font-size:12px;margin:2px 0;">${prevLine}</div>
          <div style="font-size:12px;margin:2px 0;">${overallLine}</div>
          <div style="font-size:12px;margin:2px 0;color:${OKABE_ITO_PALETTE.orange};">${durationLine}</div>
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
        left: isMobile ? '5%' : '8%',
        top: isMobile ? 48 : 56,
        bottom: 24,
        width: isMobile ? '90%' : '56%',
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
            const convText = formatConversionTag(item.conversionRate);
            const stayText = formatAvgStayTag(durationFormatter(item.avgStayMinutes));
            const countText = formatTasksCount(item.count);
            return `{title|${item.stageName}}\n{stat|${convText}  |  ${stayText}}\n{count|${countText}}`;
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
