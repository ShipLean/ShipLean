import { expect, test } from 'vitest';
import type { StateDto } from '../../../api/projects';
import { itemRow } from '../ct/fixtures';
import { removePlan, setStatePlan, setTagPlan, stateNamesFor } from './setPlans';

const state = (id: string, type: StateDto['type'], name: string, category = 'inProgress') =>
  ({ id, type, name, category, order: 1, itemCount: 0 }) as StateDto;
const states = [
  state('s-review', 'story', 'In review'),
  state('s-gone', 'story', 'Removed', 'removed'),
  state('b-active', 'bug', 'Active'),
  state('b-gone', 'bug', 'Removed', 'removed'),
];
const story = itemRow({ key: 'SHIP-1' });
const bugs = [1, 2].map((n) => itemRow({ key: `SHIP-${n + 1}`, type: { id: 'bug', name: 'Bug' } }));

test('setStatePlan_WhenTwoBugsLackTheState_ShouldCountThem', () => {
  const { plan, skipped } = setStatePlan([story, ...bugs], states, 'In review');

  expect(plan.items.map((i) => i.row.id)).toEqual([story.id]);
  expect(skipped).toBe('2 bugs have no In review');
  expect(plan.label(1)).toBe('In review on 1 item; 2 bugs have no In review');
});

test('stateNamesFor_ShouldListNamesOfSelectedTypesWithoutRemoved', () => {
  expect(stateNamesFor(bugs, states)).toEqual(['Active']);
  expect(stateNamesFor([story, ...bugs], states)).toEqual(['In review', 'Active']);
});

test('removePlan_ShouldUseEachTypesRemovedState', () => {
  const ids = removePlan([story, bugs[0]], states).plan.items.map((i) => i.changes?.state?.value);

  expect(ids).toEqual(['s-gone', 'b-gone']);
});

test('setTagPlan_ShouldAddToEachRowsOwnTags', () => {
  const tagged = itemRow({ key: 'SHIP-9', tags: [{ name: 'api', colour: null }] });
  const tags = setTagPlan([tagged, story], 'x').plan.items.map((i) => i.changes?.tags?.value);

  expect(tags).toEqual([['api', 'x'], ['x']]);
});
