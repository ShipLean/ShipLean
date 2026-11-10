import { describe, expect, it } from 'vitest';
import { aBacklogRow, designFeatures } from '../../test-support/backlog';
import { blockedWarning, plannedKeys } from './blockedBy';
import { rankPlan } from '../../actions/rankPlan';
import { rankRows } from './rankRows';

describe('blockedBy warning', () => {
  const features = designFeatures();
  const rows = rankRows(features);
  const row = (key: string) => features.find((r) => r.key === key)!;

  it('warns when the item would sit above an item it is blocked by', () => {
    const moved = row('SHIP-906');
    const plan = rankPlan(rows, moved.id, { to: 'above', otherId: row('SHIP-907').id })!;
    const planned = plannedKeys(rows, moved.id, plan.toRank);
    expect(planned.slice(0, 4)).toEqual(['SHIP-707', 'SHIP-906', 'SHIP-907', 'SHIP-892']);
    expect(blockedWarning(moved.key, moved.blockedBy, planned)).toBe(
      'SHIP-906 is blocked by SHIP-907',
    );
  });

  it('says nothing when the blocker stays above or blockedBy is empty', () => {
    const moved = row('SHIP-906');
    const down = rankPlan(rows, moved.id, { to: 'down', by: 1 })!;
    expect(
      blockedWarning(moved.key, moved.blockedBy, plannedKeys(rows, moved.id, down.toRank)),
    ).toBeNull();
    const top = rankPlan(rows, row('SHIP-256').id, { to: 'top' })!;
    const free = aBacklogRow({ key: 'SHIP-256', blockedBy: [] });
    expect(
      blockedWarning(free.key, free.blockedBy, plannedKeys(rows, row('SHIP-256').id, top.toRank)),
    ).toBeNull();
  });
});
