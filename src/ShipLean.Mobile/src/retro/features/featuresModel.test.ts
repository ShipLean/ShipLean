import { describe, expect, it } from 'vitest';
import type { RetroThemeInsightDto } from '@shiplean/core/api/retros';
import { designInsights } from '../../test-support/retro';
import {
  categoryBarParts,
  closedFixColumns,
  featureRows,
  heatMax,
  heatPercent,
  recurringThemes,
} from './featuresModel';

const withStatus = (t: RetroThemeInsightDto, status: RetroThemeInsightDto['status']) => ({
  ...t,
  status,
});

describe('retro features model', () => {
  it('keeps only recurring themes', () => {
    // designInsights() themes are all recurring, so give it one of each other status.
    const insights = designInsights();
    const [a, b, c] = insights.themes;
    insights.themes = [
      a,
      withStatus(b, 'resolved'),
      withStatus(c, 'quiet'),
      withStatus({ ...a, theme: { ...a.theme, id: 'being', name: 'Being' } }, 'beingAddressed'),
      { ...a, theme: { ...a.theme, id: 'again', name: 'Again' } },
    ];
    expect(recurringThemes(insights).map((t) => t.theme.name)).toEqual([a.theme.name, 'Again']);
  });

  it('splits the category bar in web order', () => {
    expect(
      categoryBarParts({ improve: 4, wentBadly: 3, notInPlan: 2, wentWell: 1 }).map(
        (p) => `${p.category}:${p.count}`,
      ),
    ).toEqual(['wentWell:1', 'notInPlan:2', 'wentBadly:3', 'improve:4']);
    // An empty category has no part.
    expect(
      categoryBarParts({ wentWell: 0, notInPlan: 2, wentBadly: 0, improve: 1 }).map(
        (p) => p.category,
      ),
    ).toEqual(['notInPlan', 'improve']);
  });

  it('levels heat against the largest count', () => {
    const themes = designInsights().themes;
    const max = heatMax(themes);
    expect(max).toBe(3);
    expect(heatPercent(3, max)).toBe(70);
    expect(heatPercent(0, max)).toBe(0);
    // The same count shades less when a larger count is in the view.
    expect(heatPercent(2, 3)).toBe(47);
    expect(heatPercent(2, 10)).toBe(14);
    expect(heatPercent(1, 0)).toBe(0);
    expect(heatMax([])).toBe(0);
  });

  it('finds the column a fix closed in, by theme, and lists Features before loose rows', () => {
    const insights = designInsights();
    const closed = insights.impact.find((i) => i.theme.name === 'Screenshot baselines need CI');
    expect(closedFixColumns(insights, closed!.theme.id)).toEqual([4]);
    expect(closedFixColumns(insights, 'nobody')).toEqual([]);

    const row = (key: string) => ({ item: { key } }) as never;
    expect(
      featureRows({ features: [row('F1'), row('F2')], loose: [row('L1')] }).map((r) => r.item.key),
    ).toEqual(['F1', 'F2', 'L1']);
  });
});
