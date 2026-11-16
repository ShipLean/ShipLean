import { describe, expect, it } from 'vitest';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { itemRow } from '../ct/fixtures';
import { shownOf, summarise } from './listSummary';

const done = { id: 'd', name: 'Done', category: 'completed' } as const;
const rows: BacklogRowDto[] = [
  itemRow({ key: 'SHIP-1', points: 5 }),
  itemRow({ key: 'SHIP-2', points: 8 }),
  itemRow({ key: 'SHIP-3', points: 13, state: done }),
];
const base = {
  one: 'item',
  many: 'items',
  withPoints: true,
  includeCompleted: false,
  narrowed: false,
};

describe('summarise', () => {
  it('Summarise_WhenCompletedRowsAreShown_ShouldLeaveTheirPointsOutOfOpen', () => {
    expect(summarise(rows, { ...base, includeCompleted: true })).toEqual({
      count: '3 items',
      detail: '13 pts open',
      note: '',
    });
  });
  it('Summarise_WhenCompletedHidden_ShouldSayCompletedHidden', () => {
    expect(summarise(rows, base).note).toBe('Completed hidden');
  });
  it('Summarise_WhenNarrowed_ShouldSayShowingNOfM', () => {
    expect(summarise(rows.slice(0, 2), { ...base, narrowed: true, whole: 14 }).note).toBe(
      'Showing 2 of 14',
    );
    expect(shownOf(rows.slice(0, 2), 14)).toEqual({ shown: 2, whole: 14 });
  });
  it('Summarise_WhenOneRow_ShouldUseTheSingularNoun', () => {
    expect(summarise(rows.slice(0, 1), { ...base, one: 'Epic', many: 'Epics' }).count).toBe(
      '1 Epic',
    );
  });
});
