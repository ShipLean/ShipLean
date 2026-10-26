import { describe, expect, it } from 'vitest';
import type { BoardColumnDto } from '../../../api/views';
import { filterCount, hiddenCards, toggled } from './filterQuery';

describe('filterCount', () => {
  it('counts each part of the filter once, however many people it names', () => {
    expect(filterCount({})).toBe(0);
    expect(filterCount({ q: 'login', assignee: ['a', 'b'], sprint: 's1' })).toBe(3);
    expect(filterCount({ q: '', owner: [] })).toBe(0);
  });
});

describe('toggled', () => {
  it('adds an id, removes it again and drops an empty list', () => {
    expect(toggled(undefined, 'a')).toEqual(['a']);
    expect(toggled(['a'], 'b')).toEqual(['a', 'b']);
    expect(toggled(['a', 'b'], 'a')).toEqual(['b']);
    expect(toggled(['a'], 'a')).toBeUndefined();
  });
});

describe('hiddenCards', () => {
  it('adds the hidden cards of every column', () => {
    const columns = [
      { hiddenCount: 2 },
      { hiddenCount: 0 },
      { hiddenCount: 3 },
    ] as BoardColumnDto[];
    expect(hiddenCards(columns)).toBe(5);
  });
});
