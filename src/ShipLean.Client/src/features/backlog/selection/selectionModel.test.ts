import { expect, test } from 'vitest';
import { itemRow } from '../ct/fixtures';
import { emptySelection, prune, range, selectAll, selectedRows, toggle } from './selectionModel';

// Two sections in screen order: a, b | c, d, e
const order = ['a', 'b', 'c', 'd', 'e'];

test('range_extends_from_anchor_across_sections', () => {
  const state = range(toggle(emptySelection, 'b'), order, 'd');

  expect([...state.ids]).toEqual(['b', 'c', 'd']);
  expect(state.anchor).toBe('b');
  expect([...range(toggle(emptySelection, 'd'), order, 'a').ids].sort()).toEqual([
    'a',
    'b',
    'c',
    'd',
  ]);
});

test('range_without_an_anchor_selects_just_that_row', () => {
  expect([...range(emptySelection, order, 'c').ids]).toEqual(['c']);
});

test('prune_drops_rows_that_left', () => {
  const state = range(toggle(emptySelection, 'a'), order, 'c');

  const pruned = prune(state, ['a', 'c', 'e']);

  expect([...pruned.ids]).toEqual(['a', 'c']);
  expect(prune(pruned, ['a', 'c', 'e'])).toBe(pruned);
  expect(prune(toggle(emptySelection, 'b'), ['a']).anchor).toBeNull();
});

test('selectedRows_is_in_rank_order_not_click_order', () => {
  const rows = ['SHIP-1', 'SHIP-2', 'SHIP-3'].map((key) => itemRow({ key }));
  const state = toggle(toggle(toggle(emptySelection, rows[2].id), rows[0].id), rows[1].id);

  expect(selectedRows(state, rows).map((row) => row.key)).toEqual(['SHIP-1', 'SHIP-2', 'SHIP-3']);
});

test('toggle_twice_clears_the_row_and_selectAll_takes_every_shown_row', () => {
  expect(toggle(toggle(emptySelection, 'a'), 'a').ids.size).toBe(0);
  expect(selectAll(order).ids.size).toBe(5);
});
