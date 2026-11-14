import { describe, expect, test } from 'vitest';
import { readMerge, readSheet, withMerge, withSheet } from './insightsAddress';

const q = (search: string) => new URLSearchParams(search);

describe('insightsAddress', () => {
  test('reads defs with and without an entry', () => {
    expect(readSheet(q('sheet=defs'))).toEqual({ kind: 'defs', entry: null });
    expect(readSheet(q('sheet=defs:beingAddressed'))).toEqual({
      kind: 'defs',
      entry: 'beingAddressed',
    });
    expect(readSheet(q('sheet=defs:nosprint'))).toEqual({ kind: 'defs', entry: 'nosprint' });
    expect(readSheet(q('sheet=range'))).toEqual({ kind: 'range' });
  });

  test('reads theme and merge', () => {
    expect(readSheet(q('sheet=theme:t42'))).toEqual({ kind: 'theme', themeId: 't42' });
    expect(readMerge(q('merge='))).toEqual({ themeId: null });
    expect(readMerge(q('merge=t42'))).toEqual({ themeId: 't42' });
    expect(readMerge(q('view=impact'))).toBeNull();
  });

  test('ignores unknown values', () => {
    expect(readSheet(q(''))).toBeNull();
    expect(readSheet(q('sheet=bogus'))).toBeNull();
    expect(readSheet(q('sheet=theme:'))).toBeNull();
    expect(readSheet(q('sheet=defs:bogus'))).toEqual({ kind: 'defs', entry: null });
  });

  test('writes the sheet and merge without touching the other parameters', () => {
    expect(withSheet(q('view=impact&range=w6'), 'defs:helped').toString()).toBe(
      'view=impact&range=w6&sheet=defs%3Ahelped',
    );
    expect(withSheet(q('view=impact&sheet=range'), null).toString()).toBe('view=impact');
    expect(withMerge(q('view=themes')).toString()).toBe('view=themes&merge=');
  });
});
