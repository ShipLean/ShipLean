import { afterEach, expect, test, vi } from 'vitest';
import {
  viewedKey,
  isViewed,
  readViewed,
  restampViewed,
  setViewed,
  viewedCount,
} from './viewedStore';

afterEach(() => {
  vi.restoreAllMocks();
  localStorage.clear();
});

test('readViewed_WhenStorageThrows_ShouldReadAsNothingViewed', () => {
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
    throw new Error('denied');
  });
  expect(readViewed('w1')).toEqual({});
});

test('setViewed_WhenStorageThrows_ShouldStillAnswerTheNewMarks', () => {
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
    throw new Error('full');
  });
  expect(isViewed(setViewed('w1', 'r', 'a.ts', 'p1', 'h1', true), 'r', 'a.ts', 'p1')).toBe(true);
});

test.each(['not json', '[1]', '"x"', '{"k":{"patchId":5}}'])(
  'readViewed_WhenStorageHolds_%s_ShouldReadAsNothingViewed',
  (garbage) => {
    localStorage.setItem('shiplean.taskChanges.viewed.w1', garbage);
    expect(readViewed('w1')).toEqual({});
  },
);

test('isViewed_WhenPatchIdDiffers_ShouldNotBeViewed', () => {
  const state = setViewed('w1', 'r', 'a.ts', 'p1', 'h1', true);
  expect(isViewed(state, 'r', 'a.ts', 'p1')).toBe(true);
  expect(isViewed(state, 'r', 'a.ts', 'p2')).toBe(false);
  expect(isViewed(state, 'other', 'a.ts', 'p1')).toBe(false);
});

test('restampViewed_WhenTheHeadChanges_ShouldKeepOnlyFilesWhosePatchIsUnchanged', () => {
  setViewed('w1', 'r', 'a.ts', 'p1', 'h1', true);
  setViewed('w1', 'r', 'b.ts', 'p2', 'h1', true);
  const paths = [
    { repo: 'r', path: 'a.ts' },
    { repo: 'r', path: 'b.ts' },
  ];

  const next = restampViewed(readViewed('w1'), 'h2', [
    { repo: 'r', path: 'a.ts', patchId: 'p1' },
    { repo: 'r', path: 'b.ts', patchId: 'p2-changed' },
  ]);

  expect(Object.keys(next)).toEqual([viewedKey('r', 'a.ts')]);
  expect(viewedCount(readViewed('w1'), 'h1', paths)).toBe(2);
  expect(viewedCount(next, 'h2', paths)).toBe(1);
  expect(viewedCount(next, 'h1', paths)).toBe(0);
});
