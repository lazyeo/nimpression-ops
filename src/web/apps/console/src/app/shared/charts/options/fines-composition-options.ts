import { EChartsOption } from 'echarts';
import {
  ChartThemeConfig,
  CHART_PALETTE,
  SEMANTIC_COLORS,
  LIGHT_THEME,
} from '../theme/chart-theme';

export interface FineCategoryStat {
  category: string;
  count: number;
  totalAmount: number;
  percentage: number;
}

export interface FineRankingItem {
  id: string;
  reference: string;
  category: string;
  vehicleRego: string;
  driverName: string;
  amount: number;
  issuedOn: string;
}

export interface FinesCompositionLabels {
  doughnutNoData?: string;
  rankingNoData?: string;
  rankingFilteredNoData?: string;
  doughnutSeriesName?: string;
  rankingSeriesName?: string;
  totalAmountText?: string;
  finesCountText?: string;
  shareText?: string;
  doughnutHint?: string;
  rankingTitle?: string;
  rankingTitleFiltered?: string;
  driverText?: string;
  vehicleText?: string;
  reasonText?: string;
  issuedDateText?: string;
  unassignedText?: string;
  totalCountSubtitle?: string;
  xAxisName?: string;
}

export interface FinesDoughnutOptionsParams {
  data: FineCategoryStat[];
  selectedCategory?: string | null;
  theme?: ChartThemeConfig;
  isMobile?: boolean;
  labels?: FinesCompositionLabels;
}

export interface FinesRankingBarOptionsParams {
  data: FineRankingItem[];
  selectedCategory?: string | null;
  theme?: ChartThemeConfig;
  isMobile?: boolean;
  labels?: FinesCompositionLabels;
}

/**
 * Pure function to construct ECharts options for F14.4 Fine Doughnut Chart (by category).
 */
export function buildFineDoughnutOptions(params: FinesDoughnutOptionsParams): EChartsOption {
  const { data, selectedCategory, theme = LIGHT_THEME, isMobile = false, labels = {} } = params;

  const noDataText = labels.doughnutNoData || 'No fines category data available';
  const totalAmountLabel = labels.totalAmountText || 'Total Amount';
  const finesCountLabel = labels.finesCountText || 'Fines Count';
  const shareLabel = labels.shareText || 'Proportion';
  const doughnutHintText = labels.doughnutHint || 'Click sector to filter ranking';
  const doughnutSeriesNameText = labels.doughnutSeriesName || 'Fine Breakdown';
  const totalCountSubtitleText = labels.totalCountSubtitle || 'Total {count} Fines';

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

  const grandTotalAmount = data.reduce((sum, d) => sum + d.totalAmount, 0);
  const grandTotalCount = data.reduce((sum, d) => sum + d.count, 0);

  const seriesData = data.map((d, idx) => {
    const color = CHART_PALETTE[idx % CHART_PALETTE.length];
    const isSelected = selectedCategory === d.category;

    return {
      name: d.category,
      value: d.totalAmount,
      count: d.count,
      percentage: d.percentage,
      selected: isSelected,
      itemStyle: {
        color,
        borderWidth: isSelected ? 3 : 1.5,
        borderColor: isSelected ? theme.textColor : theme.backgroundColor,
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
          data: { count: number; percentage: number };
        };
        if (!p) return '';
        return `
          <div style="font-weight:600;margin-bottom:4px;border-bottom:1px solid ${theme.tooltipBorderColor};padding-bottom:2px;">
            <span style="display:inline-block;width:10px;height:10px;border-radius:50%;background:${p.color};margin-right:6px;"></span>${p.name}
          </div>
          <div style="font-size:12px;margin:2px 0;">${totalAmountLabel}: <strong>$${p.value.toLocaleString()}</strong></div>
          <div style="font-size:12px;margin:2px 0;">${finesCountLabel}: <strong>${p.data.count}</strong></div>
          <div style="font-size:12px;margin:2px 0;">${shareLabel}: <strong>${p.data.percentage.toFixed(1)}%</strong></div>
          <div style="font-size:11px;color:${theme.textMutedColor};margin-top:4px;font-style:italic;">${doughnutHintText}</div>
        `;
      },
    },
    legend: {
      orient: isMobile ? 'horizontal' : 'vertical',
      right: isMobile ? 'center' : 8,
      top: isMobile ? 'bottom' : 'middle',
      textStyle: {
        color: theme.textColor,
        fontSize: isMobile ? 11 : 12,
      },
      formatter: (name: string) => {
        const item = data.find((d) => d.category === name);
        return item ? `${name} ($${item.totalAmount})` : name;
      },
    },
    title: {
      text: `$${grandTotalAmount.toLocaleString()}`,
      subtext: totalCountSubtitleText.replace('{count}', String(grandTotalCount)),
      left: isMobile ? 'center' : '38%',
      top: isMobile ? '38%' : '44%',
      textAlign: 'center',
      textStyle: {
        color: theme.textColor,
        fontSize: isMobile ? 16 : 20,
        fontWeight: 'bold',
      },
      subtextStyle: {
        color: theme.textSecondaryColor,
        fontSize: isMobile ? 10 : 12,
      },
    },
    series: [
      {
        name: doughnutSeriesNameText,
        type: 'pie',
        radius: isMobile ? ['38%', '62%'] : ['44%', '70%'],
        center: isMobile ? ['50%', '42%'] : ['40%', '50%'],
        avoidLabelOverlap: true,
        itemStyle: {
          borderRadius: 4,
        },
        label: {
          show: false,
        },
        emphasis: {
          scale: true,
          scaleSize: 8,
          label: {
            show: false,
          },
        },
        data: seriesData,
      },
    ],
  };

  return option;
}

/**
 * Pure function to construct ECharts options for F14.4 Fine Ranking Bar Chart (Linked).
 */
export function buildFineRankingBarOptions(params: FinesRankingBarOptionsParams): EChartsOption {
  const { data, selectedCategory, theme = LIGHT_THEME, isMobile = false, labels = {} } = params;

  const rankingNoDataText = labels.rankingNoData || 'No fine ranking data available';
  const rankingTitleText = labels.rankingTitle || 'Fine Amount Ranking TOP 10';
  const rankingTitleFilteredText =
    labels.rankingTitleFiltered || `[${selectedCategory}] Ranking TOP 10`;
  const driverLabel = labels.driverText || 'Driver';
  const vehicleLabel = labels.vehicleText || 'Vehicle';
  const reasonLabel = labels.reasonText || 'Reason';
  const issuedDateLabel = labels.issuedDateText || 'Issued Date';
  const unassignedLabel = labels.unassignedText || 'Unassigned';
  const rankingSeriesNameText = labels.rankingSeriesName || 'Fine Amount';
  const xAxisNameText = labels.xAxisName || 'Amount ($)';

  // Filter by selected category if provided
  const filteredData = selectedCategory
    ? data.filter((d) => d.category === selectedCategory)
    : data;

  // Sort by amount ascending so largest appears at the top of the horizontal bar chart
  const sorted = [...filteredData].sort((a, b) => a.amount - b.amount).slice(-10);

  if (sorted.length === 0) {
    return {
      title: {
        text: selectedCategory
          ? labels.rankingFilteredNoData || `Category "${selectedCategory}" has no ranking data`
          : rankingNoDataText,
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

  const yLabels = sorted.map((d) => `${d.vehicleRego} (${d.driverName || unassignedLabel})`);
  const values = sorted.map((d) => d.amount);

  const option: EChartsOption = {
    backgroundColor: 'transparent',
    title: {
      text: selectedCategory ? rankingTitleFilteredText : rankingTitleText,
      left: 12,
      top: 6,
      textStyle: {
        color: theme.textColor,
        fontSize: isMobile ? 12 : 14,
        fontWeight: 'bold',
      },
    },
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
        const item = sorted[items[0].dataIndex];
        if (!item) return '';

        return `
          <div style="font-weight:600;margin-bottom:4px;border-bottom:1px solid ${theme.tooltipBorderColor};padding-bottom:3px;">
            ${item.reference}
          </div>
          <div style="font-size:12px;margin:2px 0;">${vehicleLabel}: <strong>${item.vehicleRego}</strong></div>
          <div style="font-size:12px;margin:2px 0;">${driverLabel}: <strong>${item.driverName || unassignedLabel}</strong></div>
          <div style="font-size:12px;margin:2px 0;">${reasonLabel}: <strong>${item.category}</strong></div>
          <div style="font-size:12px;margin:2px 0;">${issuedDateLabel}: <strong>${item.issuedOn}</strong></div>
          <div style="font-size:13px;margin-top:4px;color:${SEMANTIC_COLORS.danger};font-weight:bold;">
            $${item.amount.toLocaleString()}
          </div>
        `;
      },
    },
    grid: {
      top: 40,
      left: isMobile ? 12 : 24,
      right: isMobile ? 40 : 56,
      bottom: 20,
      containLabel: true,
    },
    xAxis: {
      type: 'value',
      name: xAxisNameText,
      nameTextStyle: {
        color: theme.textSecondaryColor,
        fontSize: 11,
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
    yAxis: {
      type: 'category',
      data: yLabels,
      axisLine: {
        lineStyle: {
          color: theme.borderColor,
        },
      },
      axisLabel: {
        color: theme.textSecondaryColor,
        fontSize: isMobile ? 10 : 12,
        formatter: (val: string) => {
          return val.length > 14 ? `${val.substring(0, 12)}...` : val;
        },
      },
    },
    series: [
      {
        name: rankingSeriesNameText,
        type: 'bar',
        data: values,
        itemStyle: {
          color: (p: { dataIndex: number }) => {
            const item = sorted[p.dataIndex];
            // Highlight top 3 highest fines with warning/danger colors
            if (p.dataIndex >= sorted.length - 1) return SEMANTIC_COLORS.danger; // #D55E00
            if (p.dataIndex >= sorted.length - 3) return SEMANTIC_COLORS.warning; // #E69F00
            return SEMANTIC_COLORS.info; // #0072B2
          },
          borderRadius: [0, 4, 4, 0],
        },
        label: {
          show: true,
          position: 'right',
          formatter: (p: any) => `$${p?.value ?? ''}`,
          color: theme.textColor,
          fontSize: 11,
          fontWeight: 'bold',
        },
      },
    ],
  };

  return option;
}
