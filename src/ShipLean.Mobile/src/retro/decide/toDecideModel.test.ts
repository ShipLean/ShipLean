import { describe, expect, it } from 'vitest';
import { aSprintRetro } from '../../test-support/retro';
import { decideCards, logEntryCount, logSections, pointsToDecide } from './toDecideModel';

describe('to decide model', () => {
  it('orders Feature cards first and keeps undecided points only', () => {
    const [feature, ...others] = aSprintRetro().cards;
    // The fixture's Feature card has no undecided point, so it is dropped; with one added it leads, though sent last.
    const added = others.flatMap((c) => c.points).find((p) => p.decision === 'undecided')!;
    const withUndecided = {
      ...feature,
      points: [...feature.points, { ...added, id: 'feature-point' }],
    };

    expect(decideCards([feature, ...others]).map((c) => c.workItem?.key ?? null)).toEqual([
      'SHIP-141',
      'SHIP-138',
      'SHIP-129',
      null,
    ]);
    const cards = decideCards([...others, withUndecided]);
    expect(cards.map((c) => c.workItem?.key ?? null)).toEqual([
      'SHIP-101',
      'SHIP-141',
      'SHIP-138',
      'SHIP-129',
      null,
    ]);
    // Only undecided points stay: the Feature's decided and Went well points are gone.
    expect(cards[0].points.map((p) => p.id)).toEqual(['feature-point']);
    expect(cards.every((c) => c.points.every((p) => p.decision === 'undecided'))).toBe(true);
    expect(pointsToDecide(cards)).toBe(6);
  });

  it('folds the failure log into its non-empty sections', () => {
    const log = { blockers: ['a'], assumptionsDisproved: [], friction: ['b', 'c'] };
    expect(logSections(log)).toEqual([
      { label: 'Blockers', lines: ['a'] },
      { label: 'Friction', lines: ['b', 'c'] },
    ]);
    expect(logEntryCount(log)).toBe(3);
    expect(logSections({ blockers: [], assumptionsDisproved: [], friction: [] })).toEqual([]);
  });
});
