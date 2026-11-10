import { describe, expect, it } from 'vitest';
import { designBoard, designOverview } from './features';

describe('the design overview', () => {
  it('holds the design overview with Closed folded and one stale row', () => {
    const overview = designOverview();
    expect(overview.groups.map((g) => g.label)).toEqual(['Active', 'Resolved', 'New', 'Closed']);
    const closed = overview.groups[3];
    expect(closed.folded).toBe(true);
    expect(closed.rows).toEqual([]);
    expect(closed.count).toBe(2);
    expect(overview.groups.slice(0, 3).every((g) => !g.folded && g.rows.length === g.count)).toBe(
      true,
    );

    const rows = overview.groups.flatMap((g) => g.rows);
    expect(rows.filter((r) => r.statusStale)).toHaveLength(1);
    expect(rows.some((r) => r.needsYou?.onChildren === true)).toBe(true);
    expect(rows.some((r) => r.epic !== null)).toBe(true);
    expect(new Set(rows.map((r) => r.id)).size).toBe(rows.length);

    expect(designBoard().rows.some((r) => r.needsYou !== null)).toBe(true);
  });
});
