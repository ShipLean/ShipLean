import { expect, test } from 'vitest';
import { itemRow } from '../ct/fixtures';
import { countUnder } from './countUnder';

const row = itemRow({
  key: 'SHIP-103',
  rollups: [
    { type: 'story', open: 4, closed: 1 },
    { type: 'bug', open: 0, closed: 1 },
    { type: 'feature', open: 1, closed: 0 },
  ],
});

test('countUnder_WhenTypesAsked_ShouldAddTheirEntries', () => {
  expect(countUnder(row, 'story', 'bug')).toEqual({ open: 4, closed: 2, total: 6 });
  expect(countUnder(row, 'feature')).toEqual({ open: 1, closed: 0, total: 1 });
});

test('countUnder_WhenTypeHasNoEntry_ShouldCountNone', () => {
  expect(countUnder(row, 'task').total).toBe(0);
});
