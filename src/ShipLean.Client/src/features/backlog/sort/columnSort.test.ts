import { expect, test } from 'vitest';
import { itemRow } from '../ct/fixtures';
import { nextSort, sortRows } from './columnSort';

const keys = (rows: { key: string }[]) => rows.map((row) => row.key);

test('nextSort_WhenColumnClickedThreeTimes_ShouldGoAscendingDescendingThenRankOrder', () => {
  const first = nextSort(null, 'points');
  expect(first).toEqual({ column: 'points', direction: 'asc' });
  const second = nextSort(first, 'points');
  expect(second).toEqual({ column: 'points', direction: 'desc' });
  expect(nextSort(second, 'points')).toBeNull();
});

test('nextSort_WhenOtherColumnClicked_ShouldSortItAscending', () => {
  expect(nextSort({ column: 'points', direction: 'desc' }, 'title')).toEqual({
    column: 'title',
    direction: 'asc',
  });
});

test('nextSort_WhenRankClicked_ShouldGoDescendingThenRankOrder', () => {
  const first = nextSort(null, 'rank');
  expect(first).toEqual({ column: 'rank', direction: 'desc' });
  expect(nextSort(first, 'rank')).toBeNull();
});

test('sortRows_WhenRankOrder_ShouldKeepRowsAsGiven', () => {
  const rows = [itemRow({ key: 'SHIP-2', rank: 2 }), itemRow({ key: 'SHIP-1', rank: 1 })];
  expect(sortRows(rows, null)).toBe(rows);
});

test('sortRows_WhenPoints_ShouldOrderNumbersAndPutEmptyLastBothWays', () => {
  const rows = [
    itemRow({ key: 'SHIP-1', rank: 1, points: null }),
    itemRow({ key: 'SHIP-2', rank: 2, points: 8 }),
    itemRow({ key: 'SHIP-3', rank: 3, points: 2 }),
    itemRow({ key: 'SHIP-4', rank: 4, points: 13 }),
  ];
  expect(keys(sortRows(rows, { column: 'points', direction: 'asc' }))).toEqual([
    'SHIP-3',
    'SHIP-2',
    'SHIP-4',
    'SHIP-1',
  ]);
  expect(keys(sortRows(rows, { column: 'points', direction: 'desc' }))).toEqual([
    'SHIP-4',
    'SHIP-2',
    'SHIP-3',
    'SHIP-1',
  ]);
});

test('sortRows_WhenValuesTie_ShouldKeepRankOrder', () => {
  const rows = [
    itemRow({ key: 'SHIP-9', rank: 9, points: 5 }),
    itemRow({ key: 'SHIP-4', rank: 4, points: 5 }),
  ];
  expect(keys(sortRows(rows, { column: 'points', direction: 'desc' }))).toEqual([
    'SHIP-4',
    'SHIP-9',
  ]);
});

test('sortRows_WhenKey_ShouldCompareItemNumbers', () => {
  const rows = [itemRow({ key: 'SHIP-10', rank: 1 }), itemRow({ key: 'SHIP-9', rank: 2 })];
  expect(keys(sortRows(rows, { column: 'key', direction: 'asc' }))).toEqual(['SHIP-9', 'SHIP-10']);
});

test('sortRows_WhenTitle_ShouldIgnoreCaseAndCompareNumbersInText', () => {
  const rows = [
    itemRow({ key: 'SHIP-1', rank: 1, title: 'step 10' }),
    itemRow({ key: 'SHIP-2', rank: 2, title: 'Step 9' }),
    itemRow({ key: 'SHIP-3', rank: 3, title: 'apply' }),
  ];
  expect(keys(sortRows(rows, { column: 'title', direction: 'asc' }))).toEqual([
    'SHIP-3',
    'SHIP-2',
    'SHIP-1',
  ]);
});

test('sortRows_WhenState_ShouldFollowTheWorkflowBeforeTheName', () => {
  const rows = [
    itemRow({ key: 'SHIP-1', rank: 1, state: { id: 'd', name: 'Done', category: 'completed' } }),
    itemRow({ key: 'SHIP-2', rank: 2, state: { id: 'a', name: 'Active', category: 'inProgress' } }),
    itemRow({ key: 'SHIP-3', rank: 3, state: { id: 'n', name: 'New', category: 'proposed' } }),
  ];
  expect(keys(sortRows(rows, { column: 'state', direction: 'asc' }))).toEqual([
    'SHIP-3',
    'SHIP-2',
    'SHIP-1',
  ]);
});

test('sortRows_WhenSprint_ShouldGoPastCurrentFutureThenByName', () => {
  const rows = [
    itemRow({ key: 'SHIP-1', rank: 1, sprint: { id: 'f', name: 'Sprint 10', status: 'future' } }),
    itemRow({ key: 'SHIP-2', rank: 2, sprint: null }),
    itemRow({ key: 'SHIP-3', rank: 3, sprint: { id: 'c', name: 'Sprint 8', status: 'current' } }),
    itemRow({ key: 'SHIP-4', rank: 4, sprint: { id: 'g', name: 'Sprint 9', status: 'future' } }),
  ];
  expect(keys(sortRows(rows, { column: 'sprint', direction: 'asc' }))).toEqual([
    'SHIP-3',
    'SHIP-4',
    'SHIP-1',
    'SHIP-2',
  ]);
});

test('sortRows_WhenProgress_ShouldCompareTheShareDoneAndPutNothingUnderLast', () => {
  const rows = [
    itemRow({ key: 'SHIP-1', rank: 1, rollups: [] }),
    itemRow({
      key: 'SHIP-2',
      rank: 2,
      rollups: [{ type: 'story', open: 1, closed: 3, inCurrentSprint: 0, inOtherSprint: 0 }],
    }),
    itemRow({
      key: 'SHIP-3',
      rank: 3,
      rollups: [{ type: 'story', open: 3, closed: 1, inCurrentSprint: 0, inOtherSprint: 0 }],
    }),
  ];
  expect(keys(sortRows(rows, { column: 'progress', direction: 'desc' }))).toEqual([
    'SHIP-2',
    'SHIP-3',
    'SHIP-1',
  ]);
});
