import { expect, test } from 'vitest';
import type { QuickSearchGroup, QuickSearchResults } from '../types';
import { composeResults } from './composeResults';

const group = (id: QuickSearchGroup['id']): QuickSearchGroup => ({ id, title: id, rows: [] });
const retry = () => undefined;
const search: QuickSearchResults = {
  groups: [group('items')],
  status: 'error',
  note: 'search note',
  retry,
};
const recent: QuickSearchResults = {
  groups: [group('recent')],
  status: 'loading',
  note: 'recent note',
  retry,
};
const actions: QuickSearchResults = {
  groups: [group('actions')],
  status: 'ready',
  note: null,
  retry: null,
};

test('composeResults_WhenQueryEmpty_ShouldShowRecentThenActions', () => {
  // A blank query counts as empty; the search's error must not leak into the start view.
  const result = composeResults('  ', search, recent, actions);
  expect(result.groups.map((g) => g.id)).toEqual(['recent', 'actions']);
  expect(result.status).toBe('ready');
  expect(result.note).toBe('recent note');
  expect(result.retry).toBeNull();
});

test('composeResults_WhenQueryTyped_ShouldShowSearchThenActionsWithSearchStatus', () => {
  const result = composeResults('abc', search, recent, actions);
  expect(result.groups.map((g) => g.id)).toEqual(['items', 'actions']);
  expect(result.status).toBe('error');
  expect(result.note).toBe('search note');
  expect(result.retry).toBe(retry);
});
