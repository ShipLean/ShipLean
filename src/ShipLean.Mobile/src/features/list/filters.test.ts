import { describe, expect, it } from 'vitest';
import { aFeatureRow, designOverview } from '../../test-support/features';
import { anInboxItem } from '../../test-support/inbox';
import {
  chipCounts,
  chipFromView,
  epicsOf,
  groupInboxByFeature,
  NO_FILTER,
  rankMoves,
  ranksOf,
  rollupLabel,
  rollupText,
  shownGroups,
  statusBy,
} from './filters';

const shape = (groups: ReturnType<typeof shownGroups>) =>
  groups.map((g) => [g.group.stateCategory, g.count, g.rows.map((r) => r.key)]);

describe('features filters', () => {
  it('keeps group order and recounts under a filter', () => {
    const overview = designOverview();
    expect(shape(shownGroups(overview, NO_FILTER))).toEqual([
      ['inProgress', 3, ['SHIP-906', 'SHIP-743', 'SHIP-707']],
      ['resolved', 1, ['SHIP-893']],
      ['proposed', 1, ['SHIP-892']],
      ['completed', 2, []],
    ]);
    // A filter keeps the server's order and recounts what matches. The folded group sent no rows, and a group
    // nothing matches has no header.
    expect(shape(shownGroups(overview, { chip: 'atRisk', epicId: null }))).toEqual([
      ['inProgress', 1, ['SHIP-906']],
    ]);
    expect(shape(shownGroups(overview, { chip: 'blocked', epicId: null }))).toEqual([
      ['inProgress', 1, ['SHIP-743']],
    ]);
    expect(shape(shownGroups(overview, { chip: 'needsYou', epicId: null }))).toEqual([
      ['inProgress', 2, ['SHIP-906', 'SHIP-743']],
      ['resolved', 1, ['SHIP-893']],
    ]);
    const epic = overview.groups[0]!.rows[0]!.epic!;
    expect(shape(shownGroups(overview, { chip: 'all', epicId: epic.id }))).toEqual([
      ['inProgress', 2, ['SHIP-906', 'SHIP-707']],
    ]);
    expect(chipCounts(overview)).toEqual({ all: 7, needsYou: 3, atRisk: 1, blocked: 1 });
    expect(epicsOf(overview).map((e) => e.key)).toEqual(['SHIP-700']);
  });

  it('groups Inbox items under their Feature by underKey', () => {
    const [a, b, c] = [
      aFeatureRow({ key: 'SHIP-1' }),
      aFeatureRow({ key: 'SHIP-2' }),
      aFeatureRow({ key: 'SHIP-3' }),
    ];
    const story = anInboxItem({ key: 'SHIP-20', type: 'story', underKey: 'SHIP-2' });
    const own = anInboxItem({ key: 'SHIP-2', underKey: null });
    const first = anInboxItem({ key: 'SHIP-1', underKey: null });
    const stray = anInboxItem({ key: 'SHIP-90', type: 'story', underKey: null });
    const elsewhere = anInboxItem({ key: 'SHIP-91', type: 'story', underKey: 'SHIP-99' });

    const groups = groupInboxByFeature([a!, b!, c!], [story, own, stray, elsewhere, first]);

    expect(groups.map((g) => [g.feature.key, g.items.map((i) => i.key)])).toEqual([
      ['SHIP-1', ['SHIP-1']],
      ['SHIP-2', ['SHIP-2', 'SHIP-20']],
    ]);
  });

  it('reads a screen view as a chip', () => {
    expect(chipFromView('filter', { chip: 'blocked' })).toBe('blocked');
    expect(chipFromView('filter', { chip: 'epic' })).toBeNull();
    expect(chipFromView('level', { chip: 'blocked' })).toBeNull();
  });

  it('marks one row for a swap: the later of the pair, the earlier one kept', () => {
    const before = [aFeatureRow({ key: 'A', rank: 1 }), aFeatureRow({ key: 'B', rank: 2 })];
    const after = [
      aFeatureRow({ key: 'B', rank: 1 }),
      aFeatureRow({ key: 'A', rank: 2 }),
      aFeatureRow({ key: 'C', rank: 3 }),
    ];
    expect([...rankMoves(ranksOf(before), after)]).toEqual([['A', { from: 1, up: false }]]);
  });

  it('marks only the row that jumped three places, not the rows it passed', () => {
    const rank = (keys: string[]) => keys.map((key, i) => aFeatureRow({ key, rank: i + 1 }));
    const last = ranksOf(rank(['A', 'B', 'C', 'D', 'E']));

    expect([...rankMoves(last, rank(['A', 'E', 'B', 'C', 'D']))]).toEqual([
      ['E', { from: 5, up: true }],
    ]);
    expect([...rankMoves(last, rank(['B', 'C', 'D', 'A', 'E']))]).toEqual([
      ['A', { from: 1, up: false }],
    ]);
    expect([...rankMoves(last, rank(['A', 'B', 'C', 'D', 'E']))]).toHaveLength(0);
  });

  it('words the roll-ups and who wrote the status line', () => {
    expect(
      rollupText([
        { type: 'story', open: 7, closed: 2, inCurrentSprint: 0, inOtherSprint: 0 },
        { type: 'bug', open: 0, closed: 1, inCurrentSprint: 0, inOtherSprint: 0 },
        { type: 'task', open: 4, closed: 4, inCurrentSprint: 0, inOtherSprint: 0 },
        { type: 'action', open: 0, closed: 0, inCurrentSprint: 0, inOtherSprint: 0 },
      ]),
    ).toBe('2/9 stories · 1/1 bug');
    const row = designOverview().groups[0]!.rows[0]!;
    expect(statusBy(row)).toBe('Claude Code · 3h ago');
    expect(
      statusBy({
        ...row,
        statusVerified: { ...row.statusVerified!, agent: 'Claude Code', by: null },
      }),
    ).toBe('Claude Code · 3h ago');
    expect(statusBy(aFeatureRow())).toBe('');
  });

  it('words the roll-ups as S 2/9 · B 0/1', () => {
    expect(
      rollupLabel([
        { type: 'story', open: 7, closed: 2, inCurrentSprint: 0, inOtherSprint: 0 },
        { type: 'bug', open: 1, closed: 0, inCurrentSprint: 0, inOtherSprint: 0 },
        { type: 'task', open: 4, closed: 4, inCurrentSprint: 0, inOtherSprint: 0 },
      ]),
    ).toBe('S 2/9 · B 0/1');
    expect(rollupLabel([])).toBe('');
  });
});
