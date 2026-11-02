import { describe, expect, it } from 'vitest';
import { itemRow } from '../ct/fixtures';
import { groupDropOnRow, groupDropOnSection, groupPlan, landsOnSelection } from './groupDrop';
import type { BacklogSectionDto } from '../../../api/views';

const sprint = { id: 'sp1', name: 'Sprint 1', status: 'future' } as const;
const rows = ['a', 'b', 'c', 'd'].map((k, i) => itemRow({ key: `K-${k}`, rank: i + 1 }));
const inSprint = itemRow({ key: 'K-s', rank: 0, sprint });
const sections: BacklogSectionDto[] = [
  { id: 'sp1', title: 'Sprint 1', sprint, points: 0, capacity: null, rows: [inSprint] },
  { id: 'backlog', title: 'Backlog', sprint: null, points: 0, capacity: null, rows },
];
const id = (k: string) => `id-K-${k}`;

describe('groupDrop', () => {
  it('GroupDropOnRow_WhenAbove_ShouldLandAboveTheRow', () => {
    expect(groupDropOnRow(sections, new Set([id('a')]), id('c'), false)).toEqual({
      kind: 'before',
      beforeItemId: id('c'),
      sprint: null,
    });
  });

  it('GroupDropOnRow_WhenBelow_ShouldSkipSelectedRowsAfterIt', () => {
    expect(groupDropOnRow(sections, new Set([id('a'), id('c')]), id('b'), true)).toMatchObject({
      beforeItemId: id('d'),
    });
  });

  it('GroupDropOnRow_WhenBelowTheLastRow_ShouldLandLast', () => {
    expect(groupDropOnRow(sections, new Set([id('a')]), id('d'), true)).toMatchObject({
      beforeItemId: null,
    });
  });

  it('GroupDropOnRow_WhenTargetIsSelected_ShouldRefuse', () => {
    expect(landsOnSelection(new Set([id('a')]), id('a'))).toBe(true);
    expect(groupDropOnRow(sections, new Set([id('a'), id('b')]), id('b'), false)).toBeNull();
  });

  it('GroupDropOnRow_WhenInASprint_ShouldTakeThatSprint', () => {
    expect(groupDropOnRow(sections, new Set([id('a')]), id('s'), false)).toMatchObject({ sprint });
  });

  it('GroupDropOnSection_ShouldNameTheSprint', () => {
    expect(groupDropOnSection(sections, 'sp1')).toEqual({ kind: 'section', sprint });
    expect(groupDropOnSection(sections, 'nope')).toBeNull();
  });

  it('GroupPlan_WhenBefore_ShouldKeepRankOrderAndChangeOnlyRowsOutsideTheSprint', () => {
    const plan = groupPlan({ kind: 'before', beforeItemId: id('s'), sprint }, [inSprint, rows[1]]);
    expect(plan.items.map((i) => i.row.key)).toEqual(['K-s', 'K-b']);
    expect(plan.items.map((i) => i.changes === null)).toEqual([true, false]);
    expect(plan.items[0].placement).toEqual({ slot: 'before', beforeItemId: id('s') });
    expect(plan.label(2)).toBe('2 moved to Sprint 1');
  });

  it('GroupPlan_WhenLast_ShouldPlaceLast', () => {
    const plan = groupPlan({ kind: 'before', beforeItemId: null, sprint: null }, [rows[0]]);
    expect(plan.items[0].placement).toEqual({ slot: 'last', beforeItemId: null });
  });

  it('GroupPlan_WhenSection_ShouldMoveWithoutPlacement', () => {
    const plan = groupPlan({ kind: 'section', sprint }, rows.slice(0, 2));
    expect(plan.items.every((i) => i.placement === null)).toBe(true);
  });
});
