import { expect, test } from 'vitest';
import { itemRow } from '../ct/fixtures';
import { moveBeforePlan, moveToEdgePlan, moveToSprintPlan } from './movePlan';

const rows = ['SHIP-1', 'SHIP-2', 'SHIP-3'].map((key, i) => itemRow({ key, rank: i + 1 }));
const sprint = { id: 'sp25', name: 'Sprint 25', status: 'future' } as const;

test('block_keeps_rank_order', () => {
  const plan = moveToEdgePlan(rows, 'first');

  expect(plan.items.map((i) => i.row.key)).toEqual(['SHIP-1', 'SHIP-2', 'SHIP-3']);
  expect(plan.items.every((i) => i.placement?.slot === 'first' && i.changes === null)).toBe(true);
  expect(moveToEdgePlan(rows, 'last').items[0].placement).toEqual({
    slot: 'last',
    beforeItemId: null,
  });
});

test('before_uses_one_placement_for_the_block', () => {
  const target = itemRow({ key: 'SHIP-9' });

  const plan = moveBeforePlan(rows, target.id, sprint);

  expect(plan.items).toHaveLength(3);
  expect(new Set(plan.items.map((i) => JSON.stringify(i.placement)))).toEqual(
    new Set([JSON.stringify({ slot: 'before', beforeItemId: target.id })]),
  );
  expect(plan.items[0].changes?.sprint).toEqual({ value: 'sp25' });
  expect(moveBeforePlan(rows, target.id, undefined).items[0].changes).toBeNull();
});

test('before_never_puts_the_target_in_its_own_block', () => {
  expect(moveBeforePlan(rows, rows[1].id, null).items.map((i) => i.row.key)).toEqual([
    'SHIP-1',
    'SHIP-3',
  ]);
});

test('sprint_move_has_no_placement', () => {
  const inSprint = itemRow({ key: 'SHIP-4', sprint });

  const plan = moveToSprintPlan([...rows, inSprint], sprint);

  expect(plan.items.map((i) => i.row.key)).toEqual(['SHIP-1', 'SHIP-2', 'SHIP-3']);
  expect(plan.items.every((i) => i.placement === null)).toBe(true);
  expect(plan.items[0].changes?.sprint).toEqual({ value: 'sp25' });
  expect(plan.label(3)).toBe('3 moved to Sprint 25');
  expect(
    moveToSprintPlan([itemRow({ key: 'SHIP-4', sprint })], null).items[0].changes?.sprint,
  ).toEqual({ value: null });
});
