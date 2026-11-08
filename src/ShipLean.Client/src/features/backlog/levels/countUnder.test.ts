import { expect, test } from 'vitest';
import { itemRow } from '../ct/fixtures';
import { countStoryLevelUnder, countUnder } from './countUnder';

const row = itemRow({
  key: 'SHIP-103',
  rollups: [
    { type: 'story', open: 4, closed: 1, inCurrentSprint: 0, inOtherSprint: 0 },
    { type: 'bug', open: 0, closed: 1, inCurrentSprint: 0, inOtherSprint: 0 },
    { type: 'feature', open: 1, closed: 0, inCurrentSprint: 0, inOtherSprint: 0 },
  ],
});

test('countUnder_WhenTypesAsked_ShouldAddTheirEntries', () => {
  expect(countUnder(row, 'story', 'bug')).toEqual({ open: 4, closed: 2, total: 6 });
  expect(countUnder(row, 'feature')).toEqual({ open: 1, closed: 0, total: 1 });
});

test('countUnder_WhenTypeHasNoEntry_ShouldCountNone', () => {
  expect(countUnder(row, 'task').total).toBe(0);
});

test('countStoryLevelUnder sums current and other sprint counts over story-level types', () => {
  const planned = itemRow({
    key: 'SHIP-101',
    rollups: [
      { type: 'story', open: 5, closed: 2, inCurrentSprint: 2, inOtherSprint: 1 },
      { type: 'bug', open: 2, closed: 0, inCurrentSprint: 1, inOtherSprint: 1 },
      { type: 'feature', open: 1, closed: 0, inCurrentSprint: 4, inOtherSprint: 4 },
    ],
  });

  expect(countStoryLevelUnder(planned)).toEqual({
    open: 7,
    closed: 2,
    total: 9,
    current: 3,
    other: 2,
  });
});
