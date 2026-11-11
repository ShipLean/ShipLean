import { describe, expect, it } from 'vitest';
import { aBacklogRow } from '../../test-support/backlog';
import {
  backlogFilter,
  criteriaParam,
  needsLine,
  needsMeRows,
  openingLevel,
  rankMoves,
  ranksOf,
} from './filters';

const rows = [
  aBacklogRow({ key: 'SHIP-1', rank: 1 }),
  aBacklogRow({
    key: 'SHIP-2',
    rank: 2,
    needsYou: { questions: 1, decisions: 0, onChildren: false },
  }),
  aBacklogRow({ key: 'SHIP-3', rank: 3 }),
  aBacklogRow({
    key: 'SHIP-4',
    rank: 4,
    needsYou: { questions: 0, decisions: 2, onChildren: true },
  }),
];

describe('backlog filters', () => {
  it('builds the criteria JSON for epic and parent', () => {
    expect(JSON.parse(criteriaParam('features', 'e-1'))).toEqual({
      match: 'all',
      clauses: [{ field: 'epic', op: '=', value: 'e-1' }],
    });
    expect(JSON.parse(criteriaParam('stories', 'f-1'))).toEqual({
      match: 'all',
      clauses: [{ field: 'parent', op: '=', value: 'f-1' }],
    });
    expect(criteriaParam('epics', 'x')).toBe('');
    expect(criteriaParam('features', null)).toBe('');
    expect(backlogFilter('stories', { needsMe: true, open: false, parentId: null })).toEqual({
      mode: 'list',
      filter: '',
      includeCompleted: true,
    });
  });

  it('keeps rank when filtering to needs me', () => {
    const kept = needsMeRows(rows);
    expect(kept.map((r) => [r.key, r.rank])).toEqual([
      ['SHIP-2', 2],
      ['SHIP-4', 4],
    ]);
  });

  it('opens at the level its filter belongs to', () => {
    expect(openingLevel(undefined, undefined, 'SHIP-9')).toBe('stories');
    expect(openingLevel(undefined, 'SHIP-3', undefined)).toBe('features');
    expect(openingLevel('epics', undefined, undefined)).toBe('epics');
    expect(openingLevel(undefined, undefined, undefined)).toBe('features');
  });

  it('words the asks of a needs-me row', () => {
    expect(needsLine({ questions: 1, decisions: 2, onChildren: false })).toBe(
      '2 decisions · 1 question',
    );
    expect(needsLine({ questions: 3, decisions: 0, onChildren: false })).toBe('3 questions');
  });

  it('finds the rows whose rank changed since the last read', () => {
    const last = ranksOf(rows);
    const after = [
      aBacklogRow({ key: 'SHIP-1', rank: 1 }),
      aBacklogRow({ key: 'SHIP-4', rank: 2 }),
      aBacklogRow({ key: 'SHIP-2', rank: 3 }),
      aBacklogRow({ key: 'SHIP-3', rank: 4 }),
      aBacklogRow({ key: 'SHIP-9', rank: 5 }),
    ];
    expect([...rankMoves(last, after)]).toEqual([['SHIP-4', { from: 4 }]]);
  });

  it('marks one row for one move, up or down', () => {
    const last = ranksOf(rows);
    const order = (keys: string[]) => keys.map((key, i) => aBacklogRow({ key, rank: i + 1 }));

    expect([...rankMoves(last, order(['SHIP-1', 'SHIP-2', 'SHIP-4', 'SHIP-3']))]).toHaveLength(1);
    expect([...rankMoves(last, order(['SHIP-2', 'SHIP-3', 'SHIP-4', 'SHIP-1']))]).toEqual([
      ['SHIP-1', { from: 1 }],
    ]);
    expect([...rankMoves(last, order(['SHIP-4', 'SHIP-1', 'SHIP-2', 'SHIP-3']))]).toEqual([
      ['SHIP-4', { from: 4 }],
    ]);
    expect([...rankMoves(last, order(['SHIP-1', 'SHIP-2', 'SHIP-3', 'SHIP-4']))]).toHaveLength(0);
  });
});
