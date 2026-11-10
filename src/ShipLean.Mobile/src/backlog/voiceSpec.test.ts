import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import type { MemberDto } from '@shiplean/core/api/projects';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import { aBacklogRow, designFeatures, designStories } from '../test-support/backlog';
import { aTranscript } from '../test-support/voice';
import type { ParsedCall, ResolveResult, TestSetRow, VoiceFailure } from '../voice/contracts';
import { resolveCommand } from '../voice/resolve';
import type { BacklogAction } from './actions';
import { viewChange } from './list/filters';
import {
  BACKLOG_ACTIONS,
  FILTERED_MOVE_LINE,
  ONE_MOVE_WARNING,
  backlogVoiceSpec,
  type BacklogVoiceContext,
} from './voiceSpec';
import { matchTitle } from './voiceMatch';

const PROJECT_ID = '00000002-0000-4000-8000-000000000001';
const NO_FILTERS = { needsMe: false, open: false, epicKey: null, featureKey: null };

const epic = (key: string) => ({
  id: `epic-${key}`,
  key,
  type: { id: 'epic' as const, name: 'Epic' },
  title: key,
});

// The Features level of the design, the first three under SHIP-100 and the rest under SHIP-101.
const FEATURES: BacklogRowDto[] = designFeatures().map((r, i) => ({
  ...r,
  parent: epic(i < 3 ? 'SHIP-100' : 'SHIP-101'),
}));
const STORIES = designStories();
const idOf = (key: string) => FEATURES.find((f) => f.key === key)?.id;

const member = (name: string): MemberDto =>
  ({
    user: { id: name, name, initials: name.slice(0, 2), avatarColour: '#000000', photoUrl: null },
    email: `${name.toLowerCase()}@example.com`,
    role: 'member',
    status: 'active',
  }) as MemberDto;

function context(
  level: 'features' | 'stories',
  patch: Partial<BacklogVoiceContext> = {},
): BacklogVoiceContext {
  const features = FEATURES;
  const rows = level === 'features' ? FEATURES : STORIES;
  return {
    projectId: PROJECT_ID,
    level,
    rows,
    order: rows,
    filters: NO_FILTERS,
    features: features.map((f) => ({ id: f.id, key: f.key, title: f.title })),
    members: [member('Tom'), member('Priya')],
    ...patch,
  };
}

const features = () => backlogVoiceSpec(context('features'));
const stories = () => backlogVoiceSpec(context('stories'));

type Spec = ReturnType<typeof features>;

// Runs a transcript's calls through the resolver, as the voice provider does. `note` is found in the utterance.
function run(
  spec: Spec,
  utterance: string,
  calls: [string, Record<string, unknown>][],
  note?: string,
): ResolveResult<BacklogAction> {
  const span =
    note === undefined
      ? undefined
      : { start: utterance.indexOf(note), end: utterance.indexOf(note) + note.length };
  const parsed: ParsedCall[] = calls.map(([action, args]) => ({
    action,
    args,
    ...(span !== undefined && BACKLOG_ACTIONS.find((a) => a.name === action)?.noteArg !== undefined
      ? { noteSpan: span }
      : {}),
  }));
  return resolveCommand(spec, parsed, aTranscript({ text: utterance }));
}

function done(result: ResolveResult<BacklogAction>) {
  if (!result.ok) throw new Error(`Expected a preview, got ${JSON.stringify(result.failure)}`);
  return result;
}

function failed(result: ResolveResult<BacklogAction>): VoiceFailure {
  if (result.ok) throw new Error(`Expected a failure, got ${JSON.stringify(result.lines)}`);
  return result.failure;
}

const move = (a: BacklogAction | undefined) => {
  if (a === undefined || a.kind !== 'move') throw new Error('Expected a move.');
  return a;
};

describe('backlog voice spec', () => {
  it('declares ten actions plus two web-only ones', () => {
    const names = BACKLOG_ACTIONS.filter((a) => a.webOnly === undefined).map((a) => a.name);
    expect(names).toEqual([
      'move_relative',
      'move_to',
      'move_by',
      'add_item',
      'filter',
      'switch_level',
      'approve_decision',
      'add_note',
      'open',
      'ask',
    ]);
    expect(BACKLOG_ACTIONS.filter((a) => a.webOnly !== undefined).map((a) => a.name)).toEqual([
      'plan_sprint',
      'bulk_edit',
    ]);
    expect(features().actions).toBe(BACKLOG_ACTIONS);
  });

  it('gives the model the level and visible rows, and hotwords for keys, levels, people, top and bottom', () => {
    const spec = features();
    expect(spec.context()).toMatchObject({ level: 'features', people: ['Tom', 'Priya'] });
    const hot = spec.hotwords();
    expect(hot).toEqual(
      expect.arrayContaining(['SHIP-906', 'epics', 'stories', 'Tom', 'Priya', 'top', 'bottom']),
    );
  });

  it('move_relative places SHIP-906 above SHIP-907', () => {
    const r = done(
      run(features(), 'Move SHIP-906 above SHIP-907', [
        ['move_relative', { item: 'SHIP-906', relation: 'above', other: 'SHIP-907' }],
      ]),
    );
    const a = move(r.actions[0]);
    expect(a).toMatchObject({ key: 'SHIP-906', fromRank: 4, toRank: 2, projectId: PROJECT_ID });
    expect(a.placement).toEqual({ slot: 'before', beforeItemId: idOf('SHIP-907') });
    expect(a.itemId).toBe(idOf('SHIP-906'));
  });

  it('move_relative places SHIP-892 below SHIP-246', () => {
    const r = done(
      run(features(), 'Put SHIP-892 below SHIP-246', [
        ['move_relative', { item: 'SHIP-892', relation: 'below', other: 'SHIP-246' }],
      ]),
    );
    expect(r.lines[0].text).toBe('SHIP-892 3 → 6');
  });

  it('move_to puts an item at the top, the bottom or a rank', () => {
    const top = done(
      run(features(), 'Put SHIP-246 at the top', [
        ['move_to', { item: 'SHIP-246', position: 'top' }],
      ]),
    );
    expect(move(top.actions[0])).toMatchObject({ fromRank: 6, toRank: 1 });
    expect(move(top.actions[0]).placement).toEqual({ slot: 'first', beforeItemId: null });
    const bottom = done(
      run(features(), 'Send SHIP-707 to the bottom', [
        ['move_to', { item: 'SHIP-707', position: 'bottom' }],
      ]),
    );
    expect(move(bottom.actions[0])).toMatchObject({ fromRank: 1, toRank: 7 });
    const rank = done(
      run(features(), 'Move SHIP-256 to rank 2', [
        ['move_to', { item: 'SHIP-256', position: 'rank', rank: 2 }],
      ]),
    );
    expect(move(rank.actions[0])).toMatchObject({ fromRank: 7, toRank: 2 });
  });

  it('move_to refuses a rank that is not a whole number from one up', () => {
    const f = failed(
      run(features(), 'Move SHIP-256 to rank 0', [
        ['move_to', { item: 'SHIP-256', position: 'rank', rank: 0 }],
      ]),
    );
    expect(f).toEqual({
      kind: 'noValidAction',
      detail: { action: 'move_to', reason: 'invalidArgs' },
    });
  });

  it('move_to says nothing moves when the item is already there', () => {
    const f = failed(
      run(features(), 'Put SHIP-707 at the top', [
        ['move_to', { item: 'SHIP-707', position: 'top' }],
      ]),
    );
    expect(f).toEqual({ kind: 'noValidAction', detail: { action: 'move_to', reason: 'none' } });
  });

  it('move_by moves SHIP-892 down two', () => {
    const r = done(
      run(features(), 'Move SHIP-892 down two', [
        ['move_by', { item: 'SHIP-892', direction: 'down', count: 2 }],
      ]),
    );
    expect(move(r.actions[0])).toMatchObject({ fromRank: 3, toRank: 5 });
    expect(r.lines[0].text).toBe('SHIP-892 3 → 5');
  });

  it('move_by moves one place when no count is said', () => {
    const r = done(
      run(stories(), 'Move SHIP-924 down', [['move_by', { item: 'SHIP-924', direction: 'down' }]]),
    );
    expect(move(r.actions[0])).toMatchObject({ fromRank: 5, toRank: 6, level: 'stories' });
  });

  it('resolves the preview state: 4 → 2, 2 → 3, 3 → 4', () => {
    const r = done(
      run(features(), 'Move SHIP-906 above SHIP-907', [
        ['move_relative', { item: 'SHIP-906', relation: 'above', other: 'SHIP-907' }],
      ]),
    );
    expect(r.lines).toHaveLength(1);
    expect(r.lines[0].text).toBe('SHIP-906 4 → 2');
    expect(r.lines[0].detail).toBe('SHIP-907 2 → 3\nSHIP-892 3 → 4');
    expect(move(r.actions[0]).shifted).toEqual([
      { key: 'SHIP-907', from: 2, to: 3 },
      { key: 'SHIP-892', from: 3, to: 4 },
    ]);
  });

  it('warns when the move puts SHIP-906 above an item it is blocked by', () => {
    const r = done(
      run(features(), 'Move SHIP-906 above SHIP-907', [
        ['move_relative', { item: 'SHIP-906', relation: 'above', other: 'SHIP-907' }],
      ]),
    );
    expect(r.lines[0].warning).toBe('SHIP-906 is blocked by SHIP-907');
    expect(move(r.actions[0]).warning).toBe('SHIP-906 is blocked by SHIP-907');
    // Below its blocker there is nothing to warn about.
    const ok = done(
      run(features(), 'Put SHIP-906 below SHIP-891', [
        ['move_relative', { item: 'SHIP-906', relation: 'below', other: 'SHIP-891' }],
      ]),
    );
    expect(ok.lines[0].warning).toBeUndefined();
  });

  it('keeps only the first of two moves and warns', () => {
    const r = done(
      run(features(), 'Put SHIP-246 at the top and SHIP-256 at the top', [
        ['move_to', { item: 'SHIP-246', position: 'top' }],
        ['move_to', { item: 'SHIP-256', position: 'top' }],
      ]),
    );
    expect(r.actions).toHaveLength(1);
    expect(r.actions[0].key).toBe('SHIP-246');
    expect(r.lines[0].warning).toBe(ONE_MOVE_WARNING);
  });

  it('resolves the capture state: a new Story under SHIP-906 at the top, title word for word', () => {
    const utterance = 'Add a story to SHIP-906: show a banner while the models download';
    const r = done(
      run(
        stories(),
        utterance,
        [['add_item', { type: 'story', parent: 'SHIP-906' }]],
        'show a banner while the models download',
      ),
    );
    const feature = FEATURES.find((f) => f.key === 'SHIP-906');
    expect(r.actions[0]).toMatchObject({
      kind: 'addItem',
      type: 'story',
      title: 'show a banner while the models download',
      parentId: feature?.id,
      parentKey: 'SHIP-906',
      placement: { slot: 'first', beforeItemId: null },
    });
    expect(r.lines[0].text).toBe(
      'New Story: "show a banner while the models download" · under SHIP-906 · at the top',
    );
    expect(r.lines[0].detail).toBe('Everything else moves down one.');
  });

  it('puts a captured item at the bottom when the bottom is said', () => {
    const utterance = 'Add a story to SHIP-906 at the bottom: dark mode for the listening sheet';
    const r = done(
      run(
        stories(),
        utterance,
        [['add_item', { type: 'story', parent: 'SHIP-906', position: 'bottom' }]],
        'dark mode for the listening sheet',
      ),
    );
    expect(r.actions[0]).toMatchObject({ placement: { slot: 'last', beforeItemId: null } });
    expect(r.lines[0].text).toContain('at the bottom');
  });

  it('adds a Story under a Feature named by key from the context features', () => {
    const r = done(
      run(
        stories(),
        'Add a bug under SHIP-907: the sign in button does nothing',
        [['add_item', { type: 'bug', parent: 'SHIP-907' }]],
        'the sign in button does nothing',
      ),
    );
    const feature = FEATURES.find((f) => f.key === 'SHIP-907');
    expect(r.actions[0]).toMatchObject({
      type: 'bug',
      parentId: feature?.id,
      parentKey: 'SHIP-907',
    });
  });

  it('hands back a parent key the context does not hold', () => {
    const f = failed(
      run(
        stories(),
        'Add a story to SHIP-999: something',
        [['add_item', { type: 'story', parent: 'SHIP-999' }]],
        'something',
      ),
    );
    expect(f.kind).toBe('noMatch');
  });

  it('adds a Feature under an Epic at the Features level and refuses a Story there', () => {
    const utterance = 'Add a feature under SHIP-100: offline mode for the Backlog';
    const r = done(
      run(
        features(),
        utterance,
        [['add_item', { type: 'feature', parent: 'SHIP-100' }]],
        'offline mode for the Backlog',
      ),
    );
    expect(r.actions[0]).toMatchObject({
      type: 'feature',
      parentKey: 'SHIP-100',
      parentId: 'epic-SHIP-100',
    });
    const f = failed(run(features(), 'Add a story: x', [['add_item', { type: 'story' }]], 'x'));
    expect(f).toEqual({
      kind: 'noValidAction',
      detail: { action: 'add_item', reason: 'notAllowed' },
    });
  });

  it('copies a title with numbers and a key in it word for word', () => {
    const utterance = 'Add a story to SHIP-707 called fix SHIP-912 redirect and 2 retries';
    const r = done(
      run(
        stories(),
        utterance,
        [['add_item', { type: 'story', parent: 'SHIP-707' }]],
        'fix SHIP-912 redirect and 2 retries',
      ),
    );
    expect(r.actions[0]).toMatchObject({ title: 'fix SHIP-912 redirect and 2 retries' });
  });

  it('returns unclear when an add or a note has no span', () => {
    const f = failed(
      run(stories(), 'Add a story to SHIP-906', [
        ['add_item', { type: 'story', parent: 'SHIP-906' }],
      ]),
    );
    expect(f).toEqual({ kind: 'unclear', detail: { heard: 'Add a story to SHIP-906' } });
  });

  it('filter gives a screen view for the Epic chip and no state filter', () => {
    const epicChip = done(
      run(features(), 'Show epic SHIP-100', [['filter', { filter: 'SHIP-100' }]]),
    );
    expect(epicChip.actions).toEqual([]);
    expect(epicChip.view).toEqual({
      kind: 'screen',
      command: 'filter',
      args: { chip: 'epic', key: 'SHIP-100' },
    });
    if (epicChip.view?.kind !== 'screen') throw new Error('Expected a screen view.');
    expect(viewChange(epicChip.view.command, epicChip.view.args)).toEqual({
      kind: 'chip',
      chip: 'epic',
      on: true,
      key: 'SHIP-100',
    });
    const needs = done(run(features(), 'Show what needs me', [['filter', { filter: 'needsMe' }]]));
    expect(needs.view).toEqual({ kind: 'screen', command: 'filter', args: { chip: 'needsMe' } });
    const open = done(run(features(), 'Only open items', [['filter', { filter: 'open' }]]));
    expect(open.view).toEqual({ kind: 'screen', command: 'filter', args: { chip: 'open' } });
    // There is no state filter on the phone: a state name is not a key on the screen.
    const state = failed(run(features(), 'Only blocked', [['filter', { filter: 'blocked' }]]));
    expect(state.kind).toBe('noMatch');
    const byFeature = done(
      run(stories(), 'Only stories under SHIP-906', [['filter', { filter: 'SHIP-906' }]]),
    );
    expect(byFeature.view).toEqual({
      kind: 'screen',
      command: 'filter',
      args: { chip: 'feature', key: 'SHIP-906' },
    });
  });

  it('switch_level gives the level view', () => {
    const r = done(run(features(), 'Show stories', [['switch_level', { level: 'stories' }]]));
    expect(r.view).toEqual({ kind: 'screen', command: 'level', args: { level: 'stories' } });
    if (r.view?.kind !== 'screen') throw new Error('Expected a screen view.');
    expect(viewChange(r.view.command, r.view.args)).toEqual({ kind: 'level', level: 'stories' });
  });

  it('approve_decision approves the numbered decisions on the item', () => {
    const r = done(
      run(features(), 'Approve decisions 1 and 2 on SHIP-246', [
        ['approve_decision', { item: 'SHIP-246', decisions: [1, 2] }],
      ]),
    );
    expect(r.actions[0]).toMatchObject({
      area: 'backlog',
      kind: 'approve',
      key: 'SHIP-246',
      numbers: [1, 2],
    });
    expect(r.lines[0].text).toBe('Approve D1, D2 on SHIP-246');
    const none = failed(
      run(features(), 'Approve on SHIP-246', [
        ['approve_decision', { item: 'SHIP-246', decisions: [] }],
      ]),
    );
    expect(none).toEqual({
      kind: 'noValidAction',
      detail: { action: 'approve_decision', reason: 'invalidArgs' },
    });
  });

  it('add_note leaves the words of the span, not the model text', () => {
    const utterance = 'Note on SHIP-906 that SHIP-907 has to land first';
    const r = done(
      run(
        features(),
        utterance,
        [['add_note', { item: 'SHIP-906', note: 'rewritten by the model' }]],
        'that SHIP-907 has to land first',
      ),
    );
    expect(r.actions[0]).toMatchObject({
      kind: 'addNote',
      key: 'SHIP-906',
      body: 'SHIP-907 has to land first',
    });
    expect(r.lines[0].text).toBe('Note on SHIP-906: "SHIP-907 has to land first"');
  });

  it('open navigates to the item', () => {
    const r = done(run(features(), 'Open SHIP-906', [['open', { item: 'SHIP-906' }]]));
    expect(r.actions).toEqual([]);
    expect(r.view).toEqual({
      kind: 'navigate',
      link: { pathname: '/p/[key]/items/[itemKey]', params: { key: 'SHIP', itemKey: 'SHIP-906' } },
    });
  });

  it('ask answers from the visible rows', () => {
    const blocked = done(run(features(), "What's blocked?", [['ask', { topic: 'whatsBlocked' }]]));
    expect(blocked.view).toEqual({
      kind: 'answer',
      content: {
        lead: '1 item blocked',
        rows: [
          {
            key: 'SHIP-906',
            title: 'Voice commands on the mobile app with on-device models',
            counts: 'blocked by SHIP-907',
          },
        ],
      },
    });
    const needs = done(run(features(), 'What needs me?', [['ask', { topic: 'whatNeedsMe' }]]));
    expect(
      needs.view?.kind === 'answer' && needs.view.content.rows.map((r) => [r.key, r.counts]),
    ).toEqual([
      ['SHIP-907', '2 decisions'],
      ['SHIP-906', '1 question'],
      ['SHIP-246', '1 decision · 1 question'],
    ]);
    const next = done(run(features(), "What's next?", [['ask', { topic: 'whatsNext' }]]));
    expect(next.view?.kind === 'answer' && next.view.content.rows.map((r) => r.key)).toEqual([
      'SHIP-707',
      'SHIP-907',
      'SHIP-892',
    ]);
  });

  it('refuses the web-only actions with where to do it', () => {
    const f = failed(run(features(), "Let's do sprint planning", [['plan_sprint', {}]]));
    expect(f).toEqual({
      kind: 'webOnly',
      detail: { action: 'plan_sprint', where: 'Sprint planning: the web Backlog’s Planning view' },
    });
  });

  it('returns ambiguous with two numbered candidates for the which state', () => {
    const f = failed(
      run(stories(), 'Put the sign-in story at the top', [
        ['move_to', { item: 'sign-in story', position: 'top' }],
      ]),
    );
    if (f.kind !== 'ambiguous') throw new Error(`Expected ambiguous, got ${f.kind}`);
    expect(f.detail.candidates).toEqual([
      { key: 'SHIP-912', title: 'Sign in on the phone through the browser', rank: 4 },
      { key: 'SHIP-736', title: 'Sign-in screen on mobile web', rank: 6 },
    ]);
    expect(f.detail.call).toMatchObject({ action: 'move_to', args: { item: 'sign-in story' } });
  });

  it('refuses a move while the Epic or Feature filter is on with notAllowed', () => {
    for (const filters of [
      { ...NO_FILTERS, epicKey: 'SHIP-100' },
      { ...NO_FILTERS, featureKey: 'SHIP-906' },
    ]) {
      const spec = backlogVoiceSpec(context('features', { filters }));
      const f = failed(
        run(spec, 'Move SHIP-906 to the top', [['move_to', { item: 'SHIP-906', position: 'top' }]]),
      );
      expect(f).toEqual({
        kind: 'noValidAction',
        detail: { action: 'move_to', reason: 'notAllowed' },
      });
    }
    expect(FILTERED_MOVE_LINE).toBe('Clear the filter to move items.');
    // Needs me does not block moves.
    const needsMe = backlogVoiceSpec(
      context('features', { filters: { ...NO_FILTERS, needsMe: true } }),
    );
    done(
      run(needsMe, 'Move SHIP-906 to the top', [
        ['move_to', { item: 'SHIP-906', position: 'top' }],
      ]),
    );
  });

  it('hands a name that matches nothing back as no match', () => {
    const f = failed(run(features(), 'Open the zebra feature', [['open', { item: 'zebra' }]]));
    expect(f.kind).toBe('noMatch');
  });
});

describe('matchTitle', () => {
  const rows = [
    { title: 'Sign in on the phone through the browser' },
    { title: 'Sign-in screen on mobile web' },
    { title: 'Download the models' },
  ];

  it('matches every word case-insensitively', () => {
    expect(matchTitle('SIGN in', rows)).toEqual([rows[0], rows[1]]);
    expect(matchTitle('sign in phone', rows)).toEqual([rows[0]]);
    expect(matchTitle('sign in zebra', rows)).toEqual([]);
    expect(matchTitle('download the models', rows)).toEqual([rows[2]]);
  });

  it('ignores the filler words', () => {
    expect(matchTitle('the sign-in story', rows)).toEqual([rows[0], rows[1]]);
    expect(matchTitle('the models feature one', rows)).toEqual([rows[2]]);
    expect(matchTitle('the story', rows)).toEqual([]);
  });

  it('prefers a title the phrase spells out in full', () => {
    const two = [{ title: 'Models' }, { title: 'Models and more' }];
    expect(matchTitle('models', two)).toEqual([two[0]]);
  });
});

describe('backlog test set', () => {
  const rows = readFileSync(new URL('../voice/testset/backlog.jsonl', import.meta.url), 'utf8')
    .split('\n')
    .filter((l) => l !== '')
    .map((l) => JSON.parse(l) as TestSetRow);

  const FAILS: Record<string, string> = {
    'Put the sign-in story at the top': 'ambiguous',
    'Open the sign in one': 'ambiguous',
    'Add a note to the sprint feature: check the capacity first': 'ambiguous',
    'Move the mobile one to the bottom': 'ambiguous',
    "Let's do sprint planning": 'webOnly',
    'Bulk edit these': 'webOnly',
    'Move SHIP-906 to the top': 'noValidAction',
  };
  const KIND: Record<string, string> = {
    move_relative: 'move',
    move_to: 'move',
    move_by: 'move',
    add_item: 'addItem',
    approve_decision: 'approve',
    add_note: 'addNote',
  };

  it('has at least 40 rows, three per action, four ambiguous and four failures', () => {
    expect(rows.length).toBeGreaterThanOrEqual(40);
    for (const a of BACKLOG_ACTIONS.filter((x) => x.webOnly === undefined))
      expect(
        rows.filter((r) => r.calls.some((c) => c.action === a.name)).length,
      ).toBeGreaterThanOrEqual(3);
    expect(Object.values(FAILS).filter((k) => k === 'ambiguous')).toHaveLength(4);
    expect(rows.filter((r) => r.calls.length === 0).length + 1).toBeGreaterThanOrEqual(4);
  });

  it('replays every test-set row through toActions to its expected calls and note text', () => {
    for (const row of rows) {
      const level = row.context?.level === 'stories' ? 'stories' : 'features';
      const filters = { ...NO_FILTERS, ...((row.context?.filters as object | undefined) ?? {}) };
      const spec = backlogVoiceSpec(context(level, { filters }));
      const result = run(
        spec,
        row.utterance,
        row.calls.map((c) => [c.action, c.args]),
        row.note ?? undefined,
      );
      const label = row.utterance;
      if (row.calls.length === 0) {
        expect(failed(result).kind, label).toBe('unclear');
      } else if (FAILS[label] !== undefined) {
        expect(failed(result).kind, label).toBe(FAILS[label]);
      } else {
        const r = done(result);
        expect(r.rejected, label).toEqual([]);
        const [call] = row.calls;
        const kind = KIND[call.action];
        if (kind === undefined) expect(r.actions, label).toEqual([]);
        else
          expect(
            r.actions.map((a) => a.kind),
            label,
          ).toEqual([kind]);
        if (row.note !== null) {
          const a = r.actions[0];
          expect(a.kind === 'addNote' ? a.body : a.kind === 'addItem' ? a.title : null, label).toBe(
            row.note,
          );
        }
        expect(r.lines.length, label).toBe(r.actions.length);
      }
    }
  });
});

describe('a Backlog row builder', () => {
  it('is not used for a group row', () => {
    const group = aBacklogRow({ kind: 'group' as BacklogRowDto['kind'], rank: null });
    const spec = backlogVoiceSpec(context('features', { rows: [group], order: [group] }));
    const f = failed(run(spec, 'Open SHIP-1', [['open', { item: 'SHIP-1' }]]));
    expect(f.kind).toBe('noMatch');
  });
});
