import { afterEach, expect, test, vi } from 'vitest';
import type { QuickSearchItem } from '../../types';
import { readRecent, rememberRecent } from './recentItems';

const item = (n: number, title = `Item ${n}`): QuickSearchItem => ({
  key: `SHIP-${n}`,
  title,
  type: 'story',
  state: 'Active',
  stateCategory: 'inProgress',
  parentPath: [],
  projectKey: 'SHIP',
});

afterEach(() => {
  vi.restoreAllMocks();
  localStorage.clear();
});

test('rememberRecent_WhenOpened_ShouldMoveToFrontAndKeepEight', () => {
  for (let n = 1; n <= 9; n++) rememberRecent('u1', item(n), { moveToFront: true });
  rememberRecent('u1', item(5), { moveToFront: true });

  const keys = readRecent('u1').map((i) => i.key);

  // Fails if opening appends, if a reopened item is duplicated, or if the cap is not applied.
  expect(keys).toEqual([
    'SHIP-5',
    'SHIP-9',
    'SHIP-8',
    'SHIP-7',
    'SHIP-6',
    'SHIP-4',
    'SHIP-3',
    'SHIP-2',
  ]);
});

test('rememberRecent_WhenRefetchedWithANewTitle_ShouldUpdateInPlace', () => {
  rememberRecent('u1', item(1), { moveToFront: true });
  rememberRecent('u1', item(2), { moveToFront: true });

  rememberRecent('u1', item(1, 'Renamed'), { moveToFront: false });
  rememberRecent('u1', item(3), { moveToFront: false });

  // Fails if the update reorders, or if an unopened item gets stored.
  expect(readRecent('u1').map((i) => [i.key, i.title])).toEqual([
    ['SHIP-2', 'Item 2'],
    ['SHIP-1', 'Renamed'],
  ]);
});

test('readRecent_WhenStorageThrows_ShouldReturnEmpty', () => {
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
    throw new Error('blocked');
  });
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
    throw new Error('blocked');
  });

  expect(readRecent('u1')).toEqual([]);
  expect(() => rememberRecent('u1', item(1), { moveToFront: true })).not.toThrow();
});

test('readRecent_WhenAnEntryIsMalformed_ShouldDropIt', () => {
  const noTitle: Partial<QuickSearchItem> = { ...item(2) };
  delete noTitle.title;
  localStorage.setItem(
    'shiplean.quickSearch.recent.u1',
    JSON.stringify([
      item(1),
      noTitle,
      { ...item(3), type: 'saga' },
      { ...item(4), parentPath: [1] },
      null,
    ]),
  );

  expect(readRecent('u1').map((i) => i.key)).toEqual(['SHIP-1']);

  localStorage.setItem('shiplean.quickSearch.recent.u1', '{not json');
  expect(readRecent('u1')).toEqual([]);
});

test('readRecent_WhenAnotherUser_ShouldNotSeeTheirItems', () => {
  rememberRecent('u1', item(1), { moveToFront: true });

  expect(readRecent('u2')).toEqual([]);
});
