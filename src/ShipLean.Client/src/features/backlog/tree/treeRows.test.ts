import { describe, expect, it } from 'vitest';
import { treeRows } from '../ct/fixtures';
import { rollupText, visibleRows } from './treeRows';

const { rows, checkout, mobile, epic } = treeRows();
const keys = (list: typeof rows) => list.map((r) => r.key);

describe('visibleRows', () => {
  it('shows everything when nothing is collapsed', () => {
    expect(visibleRows(rows, new Set())).toHaveLength(rows.length);
  });

  it('hides what is under a collapsed Feature, nested Features included, and nothing after it', () => {
    expect(keys(visibleRows(rows, new Set([checkout.id])))).toEqual([
      'SHIP-90',
      'SHIP-160',
      'SHIP-101',
      'SHIP-102',
      'SHIP-129',
      '',
      'SHIP-170',
    ]);
  });

  it('hides a whole Epic but keeps the No parent group', () => {
    expect(keys(visibleRows(rows, new Set([epic.id])))).toEqual(['SHIP-90', '', 'SHIP-170']);
  });

  it('keeps a story the Epic holds in place of a Removed Feature visible when a sibling Feature folds', () => {
    expect(keys(visibleRows(rows, new Set([mobile.id])))).toContain('SHIP-160');
  });
});

describe('rollupText', () => {
  it('counts open stories and bugs with their open points', () => {
    expect(rollupText(checkout, false)).toBe('3 items · Σ 11 pts');
  });

  it('counts closed ones too when Completed is on', () => {
    expect(rollupText(checkout, true)).toBe('4 items · Σ 11 pts');
  });

  it('leaves the points out when nothing is estimated, and says item for one', () => {
    expect(rollupText(mobile, false)).toBe('1 item');
  });
});
