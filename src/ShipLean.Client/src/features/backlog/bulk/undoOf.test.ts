import { expect, test } from 'vitest';
import type { WriteResult } from '../../../api/workItems';
import type { BacklogRowDto } from '../../../api/views';
import { backlogView, itemRow, planningView } from '../ct/fixtures';
import { story } from '../../workItem/ct/fixtures';
import { changesOf } from './changesOf';
import { moveToBacklogEdgePlan, moveToEdgePlan, moveToSprintPlan } from './movePlan';
import { undoOf } from './undoOf';
import type { BulkOutcome } from './useBulkRun';

const SPRINT = { id: 'sp25', name: 'Sprint 25', status: 'future' } as const;
const OTHER = { id: 'sp24', name: 'Sprint 24', status: 'current' } as const;
const rows = ['A', 'B', 'C', 'D', 'E'].map((key, index) =>
  itemRow({ key, rank: index + 1, revision: 1, points: index + 1 }),
);
const view = backlogView(rows);
const written = (...keys: string[]): WriteResult[] =>
  keys.map((key) => ({ id: `id-${key}`, key, revision: 9, changes: [], notices: [] }));
const outcome = (plan: BulkOutcome['plan'], ...keys: string[]): BulkOutcome => ({
  plan,
  written: written(...keys),
  stale: [],
  viewBefore: view,
});
const pick = (...keys: string[]) => rows.filter((row) => keys.includes(row.key));

test('restores_each_rows_own_value', () => {
  const mixed = [
    { ...rows[0], sprint: OTHER },
    { ...rows[1], sprint: null },
  ];
  const before = backlogView(mixed);
  const plan = moveToSprintPlan(mixed, SPRINT);

  const undo = undoOf(before, { ...outcome(plan, 'A', 'B'), viewBefore: before });

  expect(undo.items.map((i) => i.changes?.sprint)).toEqual([{ value: 'sp24' }, { value: null }]);
  expect(undo.items.every((i) => i.placement === null)).toBe(true);
  expect(undo.items.map((i) => i.row.revision)).toEqual([9, 9]);
  expect(undo.undoable).toBe(false);
});

test('restores_the_points_of_a_row', () => {
  const plan = {
    items: pick('B', 'C').map((row) => ({
      row,
      changes: changesOf({ points: { value: 13 } }),
      placement: null,
    })),
    label: () => 'x',
    undoable: true,
  };
  const undo = undoOf(view, outcome(plan, 'B', 'C'));
  expect(undo.items.map((i) => i.changes?.points)).toEqual([{ value: 2 }, { value: 3 }]);
  expect(undo.items[0].changes?.sprint).toBeNull();
});

test('restores_a_split_run_to_both_places', () => {
  const undo = undoOf(view, outcome(moveToEdgePlan(pick('B', 'D'), 'first'), 'B', 'D'));

  expect(undo.items.map((i) => [i.row.key, i.placement])).toEqual([
    ['B', { slot: 'before', beforeItemId: 'id-C' }],
    ['D', { slot: 'before', beforeItemId: 'id-E' }],
  ]);
});

test('goes_back_as_one_block_for_a_run', () => {
  const undo = undoOf(view, outcome(moveToEdgePlan(pick('B', 'C'), 'last'), 'B', 'C'));
  expect(undo.items.map((i) => i.placement?.beforeItemId)).toEqual(['id-D', 'id-D']);
});

test('skips_rows_that_were_stale', () => {
  const undo = undoOf(view, outcome(moveToEdgePlan(pick('B', 'C'), 'first'), 'B'));

  expect(undo.items.map((i) => i.row.key)).toEqual(['B']);
  expect(undo.items[0].placement).toEqual({ slot: 'before', beforeItemId: 'id-C' });
});

test('last_when_the_run_was_at_the_bottom', () => {
  const undo = undoOf(view, outcome(moveToEdgePlan(pick('D', 'E'), 'first'), 'D', 'E'));
  expect(undo.items.map((i) => i.placement)).toEqual([
    { slot: 'last', beforeItemId: null },
    { slot: 'last', beforeItemId: null },
  ]);
});

test('restores_the_sprint_and_rank_of_a_row_moved_to_the_top_of_the_backlog', () => {
  const inSprint = [{ ...rows[2], sprint: SPRINT }];
  const before = backlogView([rows[0], rows[1], ...inSprint, rows[3], rows[4]]);

  const undo = undoOf(before, {
    ...outcome(moveToBacklogEdgePlan(inSprint, 'first'), 'C'),
    viewBefore: before,
  });

  expect(undo.items).toHaveLength(1);
  expect(undo.items[0].changes?.sprint).toEqual({ value: 'sp25' });
  expect(undo.items[0].placement).toEqual({ slot: 'before', beforeItemId: 'id-D' });
  expect(undo.items[0].row.revision).toBe(9);
});

test('restores_rows_moved_from_a_sprint_to_the_top_of_the_backlog_in_each_sprint', () => {
  const sp24 = [1, 2, 3].map((n) => itemRow({ key: `P${n}`, rank: n, sprint: OTHER }));
  const sp25 = [1, 2].map((n) => itemRow({ key: `Q${n}`, rank: n, sprint: SPRINT }));
  const backlog = [1, 2].map((n) => itemRow({ key: `B${n}`, rank: n }));
  const base = planningView();
  const [first, second, , last] = base.sections;
  const planning = {
    ...base,
    sections: [
      { ...first, rows: sp24 },
      { ...second, rows: sp25 },
      { ...base.sections[2], rows: [] },
      { ...last, rows: backlog },
    ],
  };
  const moving = [sp24[0], sp24[2], sp25[1]];

  const undo = undoOf(planning, {
    ...outcome(moveToBacklogEdgePlan(moving, 'first'), 'P1', 'P3', 'Q2'),
    viewBefore: planning,
  });

  expect(undo.items.map((i) => [i.row.key, i.changes?.sprint, i.placement])).toEqual([
    ['P1', { value: 'sp24' }, { slot: 'before', beforeItemId: 'id-P2' }],
    ['P3', { value: 'sp24' }, { slot: 'last', beforeItemId: null }],
    ['Q2', { value: 'sp25' }, { slot: 'last', beforeItemId: null }],
  ]);
});

/** Applies Undo's items one after another to the order after the move, as the server does. */
function applied(order: string[], items: ReturnType<typeof undoOf>['items']): string[] {
  return items.reduce((current, { row, placement }) => {
    const rest = current.filter((id) => id !== row.id);
    if (placement === null) return current;
    if (placement.slot === 'last') return [...rest, row.id];
    const at = rest.indexOf(placement.beforeItemId ?? '');
    return [...rest.slice(0, at), row.id, ...rest.slice(at)];
  }, order);
}

test('puts_the_original_order_back_when_a_stale_row_was_included_later', () => {
  // A and B go to the bottom; B was stale and Include it anyway wrote it afterwards, so Undo has both.
  const plan = moveToEdgePlan(pick('A', 'B'), 'last');
  const undo = undoOf(view, outcome(plan, 'A', 'B'));

  expect(undo.items.map((i) => [i.row.key, i.placement?.beforeItemId])).toEqual([
    ['A', 'id-C'],
    ['B', 'id-C'],
  ]);
  const afterMove = ['id-C', 'id-D', 'id-E', 'id-A', 'id-B'];
  expect(applied(afterMove, undo.items)).toEqual(rows.map((row: BacklogRowDto) => row.id));
});

test('restores_an_included_rows_value_from_what_the_teammate_left', () => {
  const plan = moveToSprintPlan(pick('B'), SPRINT);
  const teammate = { ...story, id: 'id-B', sprint: OTHER, points: 8, revision: 3 };

  const undo = undoOf(view, outcome(plan, 'B'), new Map([['id-B', teammate]]));

  expect(undo.items[0].changes?.sprint).toEqual({ value: 'sp24' });
});
