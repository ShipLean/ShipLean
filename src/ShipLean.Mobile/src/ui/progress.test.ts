import type { RollupCountDto } from '@shiplean/core/api/views';
import { describe, expect, it } from 'vitest';
import { countStoryLevelUnder, countUnder } from './progress';

// The web's cases (features/backlog/levels/countUnder.test.ts), on the roll-ups alone.
const rollups: RollupCountDto[] = [
  { type: 'story', open: 4, closed: 1, inCurrentSprint: 0, inOtherSprint: 0 },
  { type: 'bug', open: 0, closed: 1, inCurrentSprint: 0, inOtherSprint: 0 },
  { type: 'feature', open: 1, closed: 0, inCurrentSprint: 0, inOtherSprint: 0 },
];

describe('progress', () => {
  it('countUnder adds the entries of the types asked for', () => {
    expect(countUnder(rollups, 'story', 'bug')).toEqual({ open: 4, closed: 2, total: 6 });
    expect(countUnder(rollups, 'feature')).toEqual({ open: 1, closed: 0, total: 1 });
  });

  it('countUnder counts none for a type with no entry', () => {
    expect(countUnder(rollups, 'task').total).toBe(0);
  });

  it('countStoryLevelUnder sums current and other sprint counts over story-level types', () => {
    const planned: RollupCountDto[] = [
      { type: 'story', open: 5, closed: 2, inCurrentSprint: 2, inOtherSprint: 1 },
      { type: 'bug', open: 2, closed: 0, inCurrentSprint: 1, inOtherSprint: 1 },
      { type: 'feature', open: 1, closed: 0, inCurrentSprint: 4, inOtherSprint: 4 },
    ];

    expect(countStoryLevelUnder(planned)).toEqual({
      open: 7,
      closed: 2,
      total: 9,
      current: 3,
      other: 2,
    });
  });
});
