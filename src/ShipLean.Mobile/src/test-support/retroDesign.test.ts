import { RETRO_CATEGORIES } from '@shiplean/core/api/retros';
import { describe, expect, it } from 'vitest';
import { designInsights, designRetro, designRetroFeatures } from './retro';

describe('the design retro', () => {
  it('holds the design retro with four categories and a supported point', () => {
    const retro = designRetro();
    expect(retro.sprint.name).toBe('Sprint 25');
    expect(retro.categories.map((c) => c.category)).toEqual([...RETRO_CATEGORIES]);
    const points = retro.categories.flatMap((c) => c.groups.flatMap((g) => g.points));
    expect(points.some((p) => p.supporters.length > 0)).toBe(true);
    expect(new Set(points.map((p) => p.id)).size).toBe(points.length);
    expect(designRetro().sprint.id).not.toBe(retro.sprint.id);

    const insights = designInsights();
    expect(insights.columns).toHaveLength(6);
    expect(insights.themes).toHaveLength(3);
    expect(insights.mergeProposals).toHaveLength(1);
    expect(designRetroFeatures().features).toHaveLength(5);
  });
});
