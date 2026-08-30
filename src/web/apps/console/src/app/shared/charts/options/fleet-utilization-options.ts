import { EChartsOption } from 'echarts';
import { ChartThemeConfig, SEMANTIC_COLORS, LIGHT_THEME } from '../theme/chart-theme';

export interface FleetUtilizationItem {
  date: string;         // 'YYYY-MM-DD'
  inTransit: number;    // Vehicles actively on jobs
  idle: number;         // Active vehicles with no active job
  maintenance: number;  // Vehicles under maintenance
  totalVehicles: number;
  tasksCount: number;
}

export interface FleetUtilizationOptionsParams {
  data: FleetUtilizationItem[];
  theme?: ChartThemeConfig;
  isMobile?: boolean;
}

/**
 * Pure function to construct ECharts options for F14.1 Fleet Utilization (Stacked Bar).
 */
export function buildFleetUtilizationOptions(params: FleetUtilizationOptionsParams): EChartsOption {
  const { data, theme = LIGHT_THEME, isMobile = false } = params;

  if (!data || data.length === 0) {
    return {
      title: {
        text: '暂无车队利用率数据',
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

  const dates = data.map(d => {
    // Format date for display: 'MM-DD' on mobile or full 'YYYY-MM-DD'
    return isMobile ? d.date.substring(5) : d.date;
  });

  const inTransitData = data.map(d => d.inTransit);
  const idleData = data.map(d => d.idle);
  const maintenanceData = data.map(d => d.maintenance);

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
        const items = rawParams as Array<{
          name: string;
          seriesName: string;
          value: number;
          color: string;
          dataIndex: number;
        }>;
        if (!items || items.length === 0) return '';
        const idx = items[0].dataIndex;
        const item = data[idx];
        const dateStr = item?.date || items[0].name;
        const total = (item?.inTransit ?? 0) + (item?.idle ?? 0) + (item?.maintenance ?? 0);
        const utilRate = total > 0 ? (((item?.inTransit ?? 0) / total) * 100).toFixed(1) : '0.0';

        let html = `<div style="font-weight:600;margin-bottom:6px;border-bottom:1px solid ${theme.tooltipBorderColor};padding-bottom:4px;">${dateStr} 车队状态</div>`;
        html += `<div style="font-size:12px;margin-bottom:4px;color:${theme.textSecondaryColor};">当日任务数: <strong>${item?.tasksCount ?? 0}</strong> | 利用率: <strong>${utilRate}%</strong></div>`;
        
        items.forEach(it => {
          html += `
            <div style="display:flex;justify-content:space-between;align-items:center;margin:3px 0;gap:12px;">
              <span><span style="display:inline-block;width:10px;height:10px;border-radius:2px;background:${it.color};margin-right:6px;"></span>${it.seriesName}</span>
              <span style="font-weight:600;">${it.value} 辆</span>
            </div>
          `;
        });
        html += `<div style="font-size:11px;color:${theme.textMutedColor};margin-top:6px;font-style:italic;">💡 点击柱子下钻当日任务详情</div>`;
        return html;
      },
    },
    legend: {
      data: ['在途车辆', '闲置车辆', '维修车辆'],
      top: 8,
      right: isMobile ? 'center' : 16,
      textStyle: {
        color: theme.textColor,
        fontSize: isMobile ? 11 : 12,
      },
      icon: 'roundRect',
      itemWidth: 14,
      itemHeight: 10,
    },
    grid: {
      top: isMobile ? 50 : 56,
      left: isMobile ? 32 : 48,
      right: isMobile ? 12 : 24,
      bottom: isMobile ? 60 : 44,
      containLabel: true,
    },
    xAxis: {
      type: 'category',
      data: dates,
      axisLine: {
        lineStyle: {
          color: theme.borderColor,
        },
      },
      axisLabel: {
        color: theme.textSecondaryColor,
        fontSize: isMobile ? 10 : 12,
        interval: isMobile ? Math.max(1, Math.floor(data.length / 8)) : Math.max(0, Math.floor(data.length / 15)),
        rotate: isMobile ? 45 : 0,
      },
      axisTick: {
        alignWithLabel: true,
      },
    },
    yAxis: {
      type: 'value',
      name: '车辆数 (辆)',
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
      },
      splitLine: {
        lineStyle: {
          color: theme.splitLineColor,
          type: 'dashed',
        },
      },
    },
    series: [
      {
        name: '在途车辆',
        type: 'bar',
        stack: 'vehicles',
        data: inTransitData,
        itemStyle: {
          color: SEMANTIC_COLORS.inTransit, // #0072B2 (Okabe-Ito Blue)
          borderRadius: [0, 0, 0, 0],
        },
        emphasis: {
          focus: 'series',
          itemStyle: {
            shadowBlur: 8,
            shadowColor: 'rgba(0, 114, 178, 0.5)',
          },
        },
      },
      {
        name: '闲置车辆',
        type: 'bar',
        stack: 'vehicles',
        data: idleData,
        itemStyle: {
          color: SEMANTIC_COLORS.idle, // #56B4E9 (Okabe-Ito Sky Blue)
          borderRadius: [0, 0, 0, 0],
        },
        emphasis: {
          focus: 'series',
          itemStyle: {
            shadowBlur: 8,
            shadowColor: 'rgba(86, 180, 233, 0.5)',
          },
        },
      },
      {
        name: '维修车辆',
        type: 'bar',
        stack: 'vehicles',
        data: maintenanceData,
        itemStyle: {
          color: SEMANTIC_COLORS.maintenance, // #D55E00 (Okabe-Ito Vermilion)
          borderRadius: [3, 3, 0, 0], // Rounded top of the stack
        },
        emphasis: {
          focus: 'series',
          itemStyle: {
            shadowBlur: 8,
            shadowColor: 'rgba(213, 94, 0, 0.5)',
          },
        },
      },
    ],
  };

  return option;
}
