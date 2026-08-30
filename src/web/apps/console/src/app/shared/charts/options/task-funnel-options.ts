import { EChartsOption } from 'echarts';
import { ChartThemeConfig, OKABE_ITO_PALETTE, LIGHT_THEME } from '../theme/chart-theme';

export interface TaskFunnelStageData {
  stage: 'Draft' | 'Assigned' | 'Acknowledged' | 'InProgress' | 'Completed';
  stageName: string;          // e.g. '已创建', '已指派', '已确认', '进行中', '已完成'
  count: number;
  conversionRate: number;     // Conversion % from previous stage (100% for first stage)
  overallConversionRate: number; // Conversion % from initial Draft stage
  avgStayMinutes: number;     // Average duration spent in this state in minutes
}

export interface TaskFunnelOptionsParams {
  data: TaskFunnelStageData[];
  theme?: ChartThemeConfig;
  isMobile?: boolean;
}

export function formatDuration(minutes: number): string {
  if (minutes < 1) return '< 1 分钟';
  if (minutes < 60) return `${Math.round(minutes)} 分钟`;
  const hours = Math.floor(minutes / 60);
  const remainingMins = Math.round(minutes % 60);
  return remainingMins > 0 ? `${hours}小时${remainingMins}分` : `${hours}小时`;
}

/**
 * Pure function to construct ECharts options for F14.5 Task Funnel Chart.
 */
export function buildTaskFunnelOptions(params: TaskFunnelOptionsParams): EChartsOption {
  const { data, theme = LIGHT_THEME, isMobile = false } = params;

  if (!data || data.length === 0) {
    return {
      title: {
        text: '暂无任务漏斗数据',
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
    OKABE_ITO_PALETTE.skyBlue,       // Draft: #56B4E9
    OKABE_ITO_PALETTE.blue,          // Assigned: #0072B2
    OKABE_ITO_PALETTE.orange,        // Acknowledged: #E69F00
    OKABE_ITO_PALETTE.reddishPurple, // InProgress: #CC79A7
    OKABE_ITO_PALETTE.bluishGreen,   // Completed: #009E73
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
        const durationStr = formatDuration(stage.avgStayMinutes);

        return `
          <div style="font-weight:600;margin-bottom:6px;border-bottom:1px solid ${theme.tooltipBorderColor};padding-bottom:3px;">
            <span style="display:inline-block;width:10px;height:10px;border-radius:2px;background:${p.color};margin-right:6px;"></span>${stage.stageName} (${stage.stage})
          </div>
          <div style="font-size:12px;margin:2px 0;">当前阶段任务量: <strong>${stage.count}</strong> 单</div>
          <div style="font-size:12px;margin:2px 0;">上一阶段转化率: <strong>${stage.conversionRate.toFixed(1)}%</strong></div>
          <div style="font-size:12px;margin:2px 0;">全链路总转化率: <strong>${stage.overallConversionRate.toFixed(1)}%</strong></div>
          <div style="font-size:12px;margin:2px 0;color:${OKABE_ITO_PALETTE.orange};">⏳ 平均停留时长: <strong>${durationStr}</strong></div>
        `;
      },
    },
    legend: {
      data: data.map(d => d.stageName),
      top: 6,
      textStyle: {
        color: theme.textColor,
        fontSize: isMobile ? 11 : 12,
      },
      icon: 'roundRect',
    },
    series: [
      {
        name: '任务流程漏斗',
        type: 'funnel',
        left: isMobile ? '5%' : '12%',
        top: isMobile ? 48 : 56,
        bottom: 24,
        width: isMobile ? '90%' : '76%',
        min: 0,
        max: Math.max(...data.map(d => d.count), 1),
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
              return `${item.stageName}: ${item.count}单 (${item.conversionRate.toFixed(0)}%)`;
            }
            return `{title|${item.stageName}}\n{stat|转化率: ${item.conversionRate.toFixed(1)}%  |  均留: ${formatDuration(item.avgStayMinutes)}}\n{count|${item.count} 单}`;
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
