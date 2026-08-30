import { describe, it, expect } from 'vitest';
import {
  buildFineDoughnutOptions,
  buildFineRankingBarOptions,
  FineCategoryStat,
  FineRankingItem,
} from './fines-composition-options';
import { LIGHT_THEME, DARK_THEME } from '../theme/chart-theme';

describe('FinesCompositionOptions Pure Function (F14.4)', () => {
  const mockCategories: FineCategoryStat[] = [
    { category: '超速', count: 12, totalAmount: 2400, percentage: 48.0 },
    { category: '违停', count: 8, totalAmount: 1200, percentage: 24.0 },
    { category: '闯红灯', count: 4, totalAmount: 1400, percentage: 28.0 },
  ];

  const mockRanking: FineRankingItem[] = [
    { id: 'f1', reference: 'F-101', category: '超速', vehicleRego: 'ABC123', driverName: 'John', amount: 350, issuedOn: '2026-08-10' },
    { id: 'f2', reference: 'F-102', category: '闯红灯', vehicleRego: 'XYZ789', driverName: 'Alice', amount: 400, issuedOn: '2026-08-12' },
    { id: 'f3', reference: 'F-103', category: '超速', vehicleRego: 'ABC123', driverName: 'John', amount: 600, issuedOn: '2026-08-15' },
    { id: 'f4', reference: 'F-104', category: '违停', vehicleRego: 'DEF456', driverName: 'Bob', amount: 150, issuedOn: '2026-08-18' },
  ];

  describe('Doughnut Chart Options', () => {
    it('should return empty state when data is empty', () => {
      const opt = buildFineDoughnutOptions({ data: [] });
      expect(opt.title).toBeDefined();
      expect((opt.title as { text: string }).text).toContain('暂无罚单分类数据');
    });

    it('should calculate grand total and center title', () => {
      const opt = buildFineDoughnutOptions({ data: mockCategories });
      const title = opt.title as { text: string; subtext: string };

      expect(title.text).toBe('$5,000');
      expect(title.subtext).toBe('总计 24 张罚单');
    });

    it('should highlight selected category with thicker border', () => {
      const opt = buildFineDoughnutOptions({ data: mockCategories, selectedCategory: '超速' });
      const series = (opt.series as Array<{ data: Array<{ name: string; itemStyle: { borderWidth: number } }> }>)[0];

      const speeding = series.data.find(d => d.name === '超速');
      const parking = series.data.find(d => d.name === '违停');

      expect(speeding?.itemStyle.borderWidth).toBe(3);
      expect(parking?.itemStyle.borderWidth).toBe(1.5);
    });
  });

  describe('Linked Ranking Bar Chart Options', () => {
    it('should filter ranking items when selectedCategory is provided', () => {
      const allOpt = buildFineRankingBarOptions({ data: mockRanking });
      const filteredOpt = buildFineRankingBarOptions({ data: mockRanking, selectedCategory: '超速' });

      const allSeries = (allOpt.series as Array<{ data: number[] }>)[0];
      const filteredSeries = (filteredOpt.series as Array<{ data: number[] }>)[0];

      expect(allSeries.data).toHaveLength(4);
      expect(filteredSeries.data).toHaveLength(2); // Only 2 speeding fines
      expect(filteredSeries.data).toEqual([350, 600]); // Sorted ascending
    });

    it('should show category empty message if no fines exist for selected category', () => {
      const opt = buildFineRankingBarOptions({ data: mockRanking, selectedCategory: '公交专用道' });
      const title = opt.title as { text: string };
      expect(title.text).toContain('类别 "公交专用道" 暂无排行数据');
    });

    it('should format tooltip with driver, vehicle rego, date, and amount', () => {
      const opt = buildFineRankingBarOptions({ data: mockRanking });
      const tooltip = opt.tooltip as { formatter: (p: unknown) => string };

      const formatted = tooltip.formatter([{ dataIndex: 3 }]); // F-103 ($600)
      expect(formatted).toContain('罚单编号: F-103');
      expect(formatted).toContain('ABC123');
      expect(formatted).toContain('John');
      expect(formatted).toContain('$600');
    });
  });
});
