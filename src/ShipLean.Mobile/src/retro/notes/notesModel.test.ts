import { describe, expect, it } from 'vitest';
import { RETRO_CATEGORIES, type RetroCategory } from '@shiplean/core/api/retros';
import type { RetroAction } from '../actions';
import { aSprint } from '../../test-support/builders';
import { aPending } from '../../test-support/actions';
import { aRetroPoint, designRetro } from '../../test-support/retro';
import {
  authorLabel,
  countPoints,
  openingCategory,
  orderedColumns,
  shownSprint,
  themeGroups,
  viewFromIntent,
  withMySupport,
} from './notesModel';

const user = (id: string) => ({
  id,
  name: id,
  initials: id.slice(0, 2).toUpperCase(),
  avatarColour: '#2f7fd8',
  photoUrl: null,
});

const retroAction = (kind: 'support' | 'unsupport'): RetroAction => ({
  area: 'retro',
  kind,
  itemId: null,
  key: null,
  pointId: 'pt',
});

describe('notes model', () => {
  it('picks the first category with points to decide', () => {
    const retro = designRetro();
    // The design's Went well has nothing to decide, so Not in the plan opens.
    expect(openingCategory(retro)).toBe('notInPlan');
    const settled = (category: RetroCategory) =>
      retro.categories.map((c) => (c.category === category ? c : { ...c, toDecide: 0 }));
    expect(openingCategory({ ...retro, categories: settled('wentBadly') })).toBe('wentBadly');
    // Nothing to decide anywhere: Went well.
    expect(
      openingCategory({
        ...retro,
        categories: settled('wentWell').map((c) => ({ ...c, toDecide: 0 })),
      }),
    ).toBe('wentWell');
    // The order is the web's, whatever order the server sent.
    const reversed = { ...retro, categories: [...retro.categories].reverse() };
    expect(orderedColumns(reversed).map((c) => c.category)).toEqual([...RETRO_CATEGORIES]);
    expect(openingCategory(reversed)).toBe('notInPlan');
  });

  it('orders themes by size and points by supporters', () => {
    const [a, b, c] = ['one', 'two', 'three'].map((name) => ({
      id: name,
      name,
      tone: 'bad' as const,
    }));
    const p = (text: string, supporters: string[]) =>
      aRetroPoint({ text, supporters: supporters.map(user) });
    const column = {
      category: 'wentBadly' as const,
      toDecide: 0,
      groups: [
        {
          theme: null,
          recurring: false,
          points: [p('loose a', []), p('loose b', []), p('loose c', [])],
        },
        { theme: a, recurring: false, points: [p('a1', [])] },
        {
          theme: b,
          recurring: true,
          points: [p('b1', ['x']), p('b2', ['x', 'y', 'z']), p('b3', ['x']), p('b4', [])],
        },
        { theme: c, recurring: false, points: [p('c1', []), p('c2', [])] },
      ],
    };
    const groups = themeGroups(column);
    // Largest first, then the unthemed group last even when it is the largest.
    expect(groups.map((g) => g.theme?.name ?? null)).toEqual(['two', 'three', 'one', null]);
    // More supporters first; equal supporters keep the server's order.
    expect(groups[0].points.map((x) => x.text)).toEqual(['b2', 'b1', 'b3', 'b4']);
    expect(groups[3].points.map((x) => x.text)).toEqual(['loose a', 'loose b', 'loose c']);
  });

  it('picks the current sprint, or the ended one while the next has no points', () => {
    const current = aSprint({ id: 'cur', status: 'current' });
    const old = aSprint({ id: 'old', status: 'past', endDate: '2000-01-14' });
    const older = aSprint({ id: 'older', status: 'past', endDate: '2000-01-01' });
    const future = aSprint({ id: 'next', status: 'future' });
    const id = (s: ReturnType<typeof shownSprint>) => s?.id ?? null;
    // Not loaded yet, or with points: the current sprint.
    expect(id(shownSprint([older, old, current, future], null))).toBe('cur');
    expect(id(shownSprint([older, old, current, future], 3))).toBe('cur');
    // No point yet: the latest ended sprint.
    expect(id(shownSprint([older, old, current, future], 0))).toBe('old');
    // Nothing ended: the empty current one.
    expect(id(shownSprint([current], 0))).toBe('cur');
    // No current sprint: the latest ended one; nothing at all: none.
    expect(id(shownSprint([older, old, future], null))).toBe('old');
    expect(shownSprint([future], null)).toBeNull();
    expect(shownSprint([], null)).toBeNull();
  });

  it("counts a retro's points", () => {
    expect(countPoints(designRetro())).toBe(8);
  });

  it('takes my +1 from what is pending until the server catches up', () => {
    const me = user('me');
    const point = aRetroPoint({ id: 'pt', supporters: [user('other')], supportedByMe: false });
    const support = (kind: 'support' | 'unsupport', overrides = {}) =>
      aPending({ action: retroAction(kind), ...overrides });
    const plus = withMySupport(point, me, [support('support')]);
    expect(plus.supportedByMe).toBe(true);
    expect(plus.supporters.map((u) => u.id)).toEqual(['other', 'me']);
    const mine = aRetroPoint({ id: 'pt', supporters: [me], supportedByMe: true });
    expect(withMySupport(mine, me, [support('unsupport')]).supporters).toEqual([]);
    expect(withMySupport(point, me, [support('support', { state: 'failed' })])).toBe(point);
    expect(withMySupport(point, null, [support('support')])).toBe(point);
  });

  it('ignores a +1 the runner has already sent, since the re-read carries the truth', () => {
    const me = user('me');
    const mine = aRetroPoint({ id: 'pt', supporters: [me], supportedByMe: true });
    const sent = aPending({ action: retroAction('unsupport'), state: 'sent' });
    // My +1 was taken back elsewhere: the server's read says so and a sent entry no longer overrides it.
    const gone = aRetroPoint({ id: 'pt', supporters: [], supportedByMe: false });
    expect(
      withMySupport(gone, me, [aPending({ action: retroAction('support'), state: 'sent' })]),
    ).toBe(gone);
    expect(withMySupport(mine, me, [sent])).toBe(mine);
    // Queued, sending and uncertain entries still show.
    for (const state of ['queued', 'sending', 'uncertain'] as const) {
      const shown = withMySupport(mine, me, [
        aPending({ action: retroAction('unsupport'), state }),
      ]);
      expect(shown.supporters).toEqual([]);
    }
  });

  it('reads a voice view: a category, To decide or Features', () => {
    for (const view of [...RETRO_CATEGORIES, 'toDecide', 'features'] as const) {
      expect(viewFromIntent({ view })).toBe(view);
    }
    expect(viewFromIntent({ view: 'nowhere' })).toBeNull();
    expect(viewFromIntent({ view: 3 })).toBeNull();
    expect(viewFromIntent({})).toBeNull();
  });

  it('words an agent with its lane', () => {
    const base = aRetroPoint().author;
    expect(authorLabel({ ...base, agentName: null })).toEqual({ name: base.user.name, lane: null });
    expect(authorLabel({ ...base, agentName: 'Claude Code · lane C' })).toEqual({
      name: 'Claude Code',
      lane: 'lane C',
    });
    expect(authorLabel({ ...base, agentName: 'Claude Code', role: 'conductor' })).toEqual({
      name: 'Claude Code',
      lane: 'conductor',
    });
  });
});
