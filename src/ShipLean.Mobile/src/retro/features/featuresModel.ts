import {
  RETRO_CATEGORIES,
  type RetroCategory,
  type RetroCategoryCountsDto,
  type RetroFeatureRowDto,
  type RetroFeaturesDto,
  type RetroInsightsDto,
  type RetroThemeInsightDto,
} from '@shiplean/core/api/retros';

// Only the themes the server calls recurring, in the server's order.
export function recurringThemes(insights: RetroInsightsDto): RetroThemeInsightDto[] {
  return insights.themes.filter((t) => t.status === 'recurring');
}

// How strongly a cell is shaded, as the web's mention cell does: its count against the largest count in the view, up to
// 70 percent.
export function heatPercent(count: number, max: number): number {
  return max > 0 ? Math.round((count / max) * 70) : 0;
}

// The largest count over the given themes (0 when there are none).
export function heatMax(themes: readonly RetroThemeInsightDto[]): number {
  return Math.max(0, ...themes.flatMap((t) => t.counts));
}

// The columns a fix closed in, for one theme. A fix is only on insights.impact, by theme.
export function closedFixColumns(insights: RetroInsightsDto, themeId: string): number[] {
  return insights.impact.filter((i) => i.theme.id === themeId).map((i) => i.closedAt);
}

export interface CategoryPart {
  category: RetroCategory;
  count: number;
}

// The bar's parts in RETRO_CATEGORIES order (Went well, Not in the plan, Went badly, Can be improved); empty categories
// have no part.
export function categoryBarParts(counts: RetroCategoryCountsDto): CategoryPart[] {
  return RETRO_CATEGORIES.map((category) => ({ category, count: counts[category] })).filter(
    (part) => part.count > 0,
  );
}

// Features first, then the stories and bugs with no Feature.
export function featureRows(features: RetroFeaturesDto): RetroFeatureRowDto[] {
  return [...features.features, ...features.loose];
}

export const toDecide = (row: RetroFeatureRowDto): boolean => row.totals.toDecide > 0;
