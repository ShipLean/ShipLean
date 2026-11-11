import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import type { InboxItemDto } from '@shiplean/core/api/inbox';
import { links } from '../links';
import { aMember } from '../test-support/builders';
import { aFeatureStates, aFeatureRow, designOverview } from '../test-support/features';
import { aDecisionAsk, anInboxItem, aQuestionAsk } from '../test-support/inbox';
import { aTranscript } from '../test-support/voice';
import type { ParsedCall, ResolveResult, TestSetRow, VoiceFailure } from '../voice/contracts';
import { resolveCommand } from '../voice/resolve';
import type { FeaturesAction } from './actions';
import { chipFromView } from './list/filters';
import {
  BOARD_ACTIONS,
  FEATURES_ACTIONS,
  featuresVoiceSpec,
  type FeaturesVoiceContext,
} from './voiceSpec';

const PROJECT_ID = '00000002-0000-4000-8000-000000000001';
const ROWS = designOverview().groups.flatMap((g) => g.rows);
const byRank = [...ROWS].sort((a, b) => a.rank - b.rank);
const row = (key: string): FeatureRowDto => {
  const found = ROWS.find((r) => r.key === key);
  if (found === undefined) throw new Error(`No ${key} in the design.`);
  return found;
};

const decisions = (key: string, numbers: number[]): InboxItemDto =>
  anInboxItem({
    id: row(key).id,
    key,
    asks: numbers.map((n) => aDecisionAsk({ number: n, question: `Decision ${n}?` })),
  });
const questions = (key: string, numbers: number[]): InboxItemDto =>
  anInboxItem({
    id: row(key).id,
    key,
    asks: numbers.map((n) => aQuestionAsk({ number: n, text: `Question ${n}?` })),
  });

const ASKS = [
  decisions('SHIP-743', [1, 2, 3, 4, 5, 6, 7, 8]),
  decisions('SHIP-893', [1, 2]),
  questions('SHIP-906', [1]),
  questions('SHIP-707', [1, 2]),
];

function context(patch: Partial<FeaturesVoiceContext> = {}): FeaturesVoiceContext {
  return {
    projectId: PROJECT_ID,
    board: false,
    focus: null,
    rows: ROWS,
    order: byRank,
    states: aFeatureStates(),
    asks: ASKS,
    members: [aMember(), aMember()],
    projectKey: 'SHIP',
    filtering: false,
    ...patch,
  };
}

const spec = (patch: Partial<FeaturesVoiceContext> = {}) => featuresVoiceSpec(context(patch));
type Spec = ReturnType<typeof spec>;

function run(
  s: Spec,
  utterance: string,
  calls: [string, Record<string, unknown>][],
  note?: string,
): ResolveResult<FeaturesAction> {
  const at = note === undefined ? -1 : utterance.indexOf(note);
  const parsed: ParsedCall[] = calls.map(([action, args]) => ({
    action,
    args,
    ...(note !== undefined && s.actions.find((a) => a.name === action)?.noteArg !== undefined
      ? { noteSpan: { start: at, end: at + note.length } }
      : {}),
  }));
  return resolveCommand(s, parsed, aTranscript({ text: utterance }));
}

function done(result: ResolveResult<FeaturesAction>) {
  if (!result.ok) throw new Error(`Expected a preview, got ${JSON.stringify(result.failure)}`);
  return result;
}

function failed(result: ResolveResult<FeaturesAction>): VoiceFailure {
  if (result.ok) throw new Error(`Expected a failure, got ${JSON.stringify(result.lines)}`);
  return result.failure;
}

describe('features voice spec', () => {
  it('declares ten actions plus two web-only ones and no read it out', () => {
    const names = FEATURES_ACTIONS.filter((a) => a.webOnly === undefined).map((a) => a.name);
    expect(names).toEqual([
      'ask_status',
      'ask_changed',
      'set_state',
      'add_note',
      'set_status',
      'set_health',
      'answer',
      'approve',
      'move_rank',
      'filter_open',
    ]);
    expect(FEATURES_ACTIONS.filter((a) => a.webOnly !== undefined).map((a) => a.name)).toEqual([
      'move_tasks',
      'create_feature',
    ]);
    expect(FEATURES_ACTIONS.some((a) => /read|speak|aloud/i.test(a.name))).toBe(false);
    expect(spec().actions).toBe(FEATURES_ACTIONS);
  });

  it('offers no move_rank and no filter_open on one Feature’s board', () => {
    const s = spec({ board: true, focus: row('SHIP-906') });
    expect(s.actions).toBe(BOARD_ACTIONS);
    expect(s.actions.map((a) => a.name)).not.toContain('move_rank');
    expect(s.actions.map((a) => a.name)).not.toContain('filter_open');
    const r = failed(
      run(s, 'Move SHIP-893 above SHIP-906', [
        ['move_rank', { feature: 'SHIP-893', relation: 'above', other: 'SHIP-906' }],
      ]),
    );
    expect(r).toMatchObject({ kind: 'noValidAction', detail: { reason: 'none' } });
  });

  it('gives the model the focus, rows and state names, and hotwords for keys, states, health, people and decisions', () => {
    const s = spec({ focus: row('SHIP-906') });
    expect(s.context()).toMatchObject({
      key: 'SHIP-906',
      rows: ROWS.map((r) => ({ key: r.key, title: r.title })),
      states: ['New', 'Active', 'Resolved', 'Closed', 'Removed'],
    });
    expect(spec().context()).toMatchObject({ key: null });
    const hot = s.hotwords();
    expect(hot).toEqual(
      expect.arrayContaining(['SHIP-906', 'Active', 'on track', 'at risk', 'blocked', 'D1', 'D8']),
    );
    expect(hot).not.toContain('D9');
  });

  it('ask_status answers on screen and changes nothing', () => {
    const r = done(run(spec(), 'What is blocked', [['ask_status', { topic: 'blocked' }]]));
    expect(r.actions).toEqual([]);
    expect(r.view).toMatchObject({
      kind: 'answer',
      content: { lead: '1 blocked: SHIP-743', rows: [{ key: 'SHIP-743' }] },
    });
    const how = done(
      run(spec(), 'How far is SHIP-906', [
        ['ask_status', { topic: 'howFar', feature: 'SHIP-906' }],
      ]),
    );
    expect(how.view).toMatchObject({
      content: { rows: [{ key: 'SHIP-906', counts: '2/9 stories' }] },
    });
  });

  it('ask_status on the tab answers over every row even with a Feature expanded', () => {
    const r = done(
      run(spec({ focus: row('SHIP-906') }), 'What is blocked', [
        ['ask_status', { topic: 'blocked' }],
      ]),
    );
    expect(r.view).toMatchObject({ content: { lead: '1 blocked: SHIP-743' } });
    const board = done(
      run(spec({ focus: row('SHIP-906'), board: true }), 'What is blocked', [
        ['ask_status', { topic: 'blocked' }],
      ]),
    );
    expect(board.view).toMatchObject({ content: { lead: 'Nothing is blocked.' } });
  });

  it('ask_status says how far a closed Feature got on its own board', () => {
    const closedState = aFeatureStates().find((st) => st.category === 'completed');
    if (closedState === undefined) throw new Error('No closed state.');
    const closed = aFeatureRow({
      key: 'SHIP-742',
      title: 'GitHub issues sync',
      state: { id: closedState.id, name: closedState.name, category: 'completed' },
    });
    const s = spec({ focus: closed, rows: [closed], order: [closed], board: true });
    const r = done(run(s, 'How far is it', [['ask_status', { topic: 'howFar' }]]));
    expect(r.view).toMatchObject({ content: { rows: [{ key: 'SHIP-742' }] } });
  });

  it('ask_changed opens the digest', () => {
    const r = done(
      run(spec(), 'What changed since Monday', [['ask_changed', { since: 'Monday' }]]),
    );
    expect(r.actions).toEqual([]);
    expect(r.view).toEqual({ kind: 'navigate', link: links.digest('SHIP') });
  });

  it('set_state moves SHIP-892 to Active', () => {
    const r = done(
      run(spec(), 'Move SHIP-892 to Active', [
        ['set_state', { feature: 'SHIP-892', state: 'Active' }],
      ]),
    );
    expect(r.actions).toEqual([
      expect.objectContaining({
        kind: 'setState',
        projectId: PROJECT_ID,
        key: 'SHIP-892',
        stateName: 'Active',
        fromStateName: 'New',
        closing: false,
      }),
    ]);
    expect(r.lines[0].text).toBe('SHIP-892: New → Active');
  });

  it('set_state fails with no such state for a state the Features do not have', () => {
    const f = failed(
      run(spec(), 'Move SHIP-892 to Limbo', [
        ['set_state', { feature: 'SHIP-892', state: 'Limbo' }],
      ]),
    );
    expect(f).toMatchObject({ kind: 'noValidAction', detail: { reason: 'noSuchState' } });
  });

  it('warns that stories are still open when closing by voice', () => {
    const r = done(
      run(spec(), 'Close SHIP-906', [['set_state', { feature: 'SHIP-906', state: 'Closed' }]]),
    );
    expect(r.actions[0]).toMatchObject({ closing: true, openStories: 7 });
    expect(r.lines[0].warning).toBe('7 still open under it');
    const quiet = done(
      run(spec(), 'Close SHIP-893', [['set_state', { feature: 'SHIP-893', state: 'Closed' }]]),
    );
    expect(quiet.lines[0].warning).toBeUndefined();
  });

  it('add_note posts the note word for word', () => {
    const text = 'the 4 GB fallback needs a device test';
    const r = done(
      run(spec(), `Add a note to SHIP-906: ${text}`, [['add_note', { feature: 'SHIP-906' }]], text),
    );
    expect(r.actions).toEqual([
      {
        area: 'features',
        kind: 'addNote',
        itemId: row('SHIP-906').id,
        key: 'SHIP-906',
        body: text,
      },
    ]);
    expect(r.lines[0].text).toBe(`Note on SHIP-906: "${text}"`);
  });

  it('set_status sets the status line and keeps the old one for Undo', () => {
    const text = 'retro and insights are in review';
    const r = done(
      run(
        spec(),
        `Set the status of SHIP-707 to ${text}`,
        [['set_status', { feature: 'SHIP-707' }]],
        text,
      ),
    );
    expect(r.actions).toEqual([
      expect.objectContaining({
        kind: 'setStatus',
        projectId: PROJECT_ID,
        text,
        from: 'Retro and insights screens built; touch drag is in review.',
      }),
    ]);
    expect(r.lines).toHaveLength(1);
  });

  it('refuses a status longer than 500 characters', () => {
    const text = 'x '.repeat(260).trim();
    const f = failed(
      run(spec(), `Status for SHIP-707 ${text}`, [['set_status', { feature: 'SHIP-707' }]], text),
    );
    expect(f).toMatchObject({ kind: 'noValidAction', detail: { reason: 'invalidArgs' } });
  });

  it('resolves the status-update state: the status word for word plus a removable health Blocked line', () => {
    const text = 'blocked on the artifact upload design';
    const r = done(
      run(spec(), `Set SHIP-892 status: ${text}`, [['set_status', { feature: 'SHIP-892' }]], text),
    );
    expect(r.actions.map((a) => a.kind)).toEqual(['setStatus', 'setHealth']);
    expect(r.actions[0]).toMatchObject({ text });
    expect(r.actions[1]).toMatchObject({ key: 'SHIP-892', health: 'blocked', from: null });
    expect(r.lines.map((l) => l.text)).toEqual([
      `New status: "${text}"`,
      'SHIP-892 health: Not set → Blocked',
    ]);
    expect(r.lines[1]).toMatchObject({ removable: true });
    expect(r.lines[1].id).not.toBe(r.lines[0].id);
    expect(r.lines).toHaveLength(r.actions.length);
  });

  it('proposes no health line when the status says blocked and health already is, or when it does not say it', () => {
    const text = 'blocked until the new host arrives';
    const already = done(
      run(spec(), `SHIP-743 status ${text}`, [['set_status', { feature: 'SHIP-743' }]], text),
    );
    expect(already.actions.map((a) => a.kind)).toEqual(['setStatus']);
    const unblocked = 'unblocked now the host is here';
    const word = done(
      run(
        spec(),
        `SHIP-892 status ${unblocked}`,
        [['set_status', { feature: 'SHIP-892' }]],
        unblocked,
      ),
    );
    expect(word.actions.map((a) => a.kind)).toEqual(['setStatus']);
  });

  it('set_health marks SHIP-707 at risk', () => {
    const r = done(
      run(spec(), 'Mark SHIP-707 at risk', [
        ['set_health', { feature: 'SHIP-707', health: 'atRisk' }],
      ]),
    );
    expect(r.actions).toEqual([
      expect.objectContaining({
        kind: 'setHealth',
        projectId: PROJECT_ID,
        key: 'SHIP-707',
        health: 'atRisk',
        from: 'onTrack',
      }),
    ]);
    expect(r.lines[0].text).toBe('SHIP-707 health: On track → At risk');
  });

  it('answer posts on the one open question and says who reads it', () => {
    const text = 'use the 4 GB fallback';
    const r = done(
      run(
        spec(),
        `Reply to the question on SHIP-906: ${text}`,
        [['answer', { feature: 'SHIP-906' }]],
        text,
      ),
    );
    expect(r.actions).toEqual([
      {
        area: 'features',
        kind: 'answer',
        itemId: row('SHIP-906').id,
        key: 'SHIP-906',
        number: 1,
        body: text,
      },
    ]);
    expect(r.lines[0].detail).toBe('Posted on SHIP-906. The next agent to pick it up reads it.');
  });

  it('answer asks which question when several are open, and lists the open ones for a closed number', () => {
    const text = 'ship it on Friday';
    const said = `Answer SHIP-707 with ${text}`;
    const f = failed(run(spec(), said, [['answer', { feature: 'SHIP-707' }]], text));
    expect(f).toMatchObject({
      kind: 'ambiguous',
      detail: { candidates: [{ key: 'Q1' }, { key: 'Q2' }] },
    });
    const closed = failed(
      run(spec(), said, [['answer', { feature: 'SHIP-707', question: 5 }]], text),
    );
    expect(closed).toMatchObject({ kind: 'ambiguous' });
    const picked = done(
      run(spec(), said, [['answer', { feature: 'SHIP-707', question: 2 }]], text),
    );
    expect(picked.actions[0]).toMatchObject({ kind: 'answer', number: 2 });
  });

  it('approve expands D1 to D8 on SHIP-743', () => {
    const r = done(
      run(spec(), 'Approve D1 to D8 on SHIP-743', [
        ['approve', { feature: 'SHIP-743', decisions: [1, 2, 3, 4, 5, 6, 7, 8] }],
      ]),
    );
    expect(r.actions).toEqual([
      {
        area: 'features',
        kind: 'approve',
        itemId: row('SHIP-743').id,
        key: 'SHIP-743',
        numbers: [1, 2, 3, 4, 5, 6, 7, 8],
      },
    ]);
    expect(r.lines[0].text).toBe('Approve D1, D2, D3, D4, D5, D6, D7, D8 on SHIP-743');
    expect(r.lines[0].detail).toBe('Posted on SHIP-743. The next agent to pick it up reads it.');
  });

  it('approve lists the open decisions for a closed or unknown number', () => {
    const f = failed(
      run(spec(), 'Approve D9 on SHIP-893', [
        ['approve', { feature: 'SHIP-893', decisions: [1, 9] }],
      ]),
    );
    expect(f).toMatchObject({
      kind: 'ambiguous',
      detail: { candidates: [{ key: 'D1' }, { key: 'D2' }] },
    });
  });

  it('move_rank moves SHIP-893 above SHIP-906', () => {
    const r = done(
      run(spec(), 'Move SHIP-893 above SHIP-906', [
        ['move_rank', { feature: 'SHIP-893', relation: 'above', other: 'SHIP-906' }],
      ]),
    );
    expect(r.actions[0]).toMatchObject({
      kind: 'moveRank',
      projectId: PROJECT_ID,
      key: 'SHIP-893',
      fromRank: 4,
      toRank: 1,
      placement: { slot: 'before', beforeItemId: row('SHIP-906').id },
    });
  });

  it('resolves the move-preview state with the rank line', () => {
    const r = done(
      run(spec(), 'Move SHIP-893 above SHIP-906', [
        ['move_rank', { feature: 'SHIP-893', relation: 'above', other: 'SHIP-906' }],
      ]),
    );
    expect(r.lines[0].text).toBe('SHIP-893 4 → 1');
    expect(r.lines[0].detail).toBe('SHIP-906 1 → 2\nSHIP-743 2 → 3\nSHIP-707 3 → 4');
  });

  it('refuses move_rank while a filter is on', () => {
    const f = failed(
      run(spec({ filtering: true }), 'Move SHIP-893 above SHIP-906', [
        ['move_rank', { feature: 'SHIP-893', relation: 'above', other: 'SHIP-906' }],
      ]),
    );
    expect(f).toMatchObject({ kind: 'noValidAction', detail: { reason: 'notAllowed' } });
  });

  it('filter_open turns on a chip the tab understands, or opens a board', () => {
    const r = done(run(spec(), 'Show what needs me', [['filter_open', { filter: 'needsMe' }]]));
    expect(r.actions).toEqual([]);
    expect(r.view).toMatchObject({ kind: 'screen', command: 'filter' });
    if (r.view?.kind !== 'screen') throw new Error('Expected a screen view.');
    expect(chipFromView(r.view.command, r.view.args)).toBe('needsYou');
    const board = done(
      run(spec(), 'Open the board for SHIP-906', [
        ['filter_open', { board: true, feature: 'SHIP-906' }],
      ]),
    );
    expect(board.view).toEqual({ kind: 'navigate', link: links.featureBoard('SHIP', 'SHIP-906') });
  });

  it('leaves the key out when a Feature is focused', () => {
    const focus = row('SHIP-906');
    const s = spec({ focus, board: true });
    const state = done(run(s, 'Move it to Resolved', [['set_state', { state: 'Resolved' }]]));
    expect(state.actions[0]).toMatchObject({ key: 'SHIP-906', stateName: 'Resolved' });
    const health = done(run(s, 'Mark it blocked', [['set_health', { health: 'blocked' }]]));
    expect(health.actions[0]).toMatchObject({ key: 'SHIP-906', health: 'blocked' });
    const text = 'blocked on the licence';
    const status = done(run(s, `Status ${text}`, [['set_status', {}]], text));
    expect(status.actions.map((a) => a.kind)).toEqual(['setStatus', 'setHealth']);
    const named = done(
      run(s, 'Mark SHIP-707 at risk', [['set_health', { feature: 'SHIP-707', health: 'atRisk' }]]),
    );
    expect(named.actions[0]).toMatchObject({ key: 'SHIP-707' });
  });

  it('asks which Feature when none is focused', () => {
    const f = failed(run(spec(), 'Mark it blocked', [['set_health', { health: 'blocked' }]]));
    expect(f).toMatchObject({ kind: 'ambiguous' });
    if (f.kind !== 'ambiguous') throw new Error('Expected ambiguous.');
    expect(f.detail.candidates.map((c) => c.key)).toEqual(ROWS.map((r) => r.key));
  });

  it('hands an unknown key back as no match and a misheard key as a suggestion', () => {
    const none = failed(
      run(spec(), 'Mark SHIP-999 blocked', [
        ['set_health', { feature: 'SHIP-999', health: 'blocked' }],
      ]),
    );
    expect(['noMatch', 'misheardKey']).toContain(none.kind);
    const heard = failed(
      run(spec(), 'Mark ship nine oh seven blocked', [
        ['set_health', { feature: 'ship nine oh seven', health: 'blocked' }],
      ]),
    );
    expect(['misheardKey', 'noMatch']).toContain(heard.kind);
  });

  it('says where to do the two web-only actions', () => {
    expect(failed(run(spec(), 'Move the task to Done', [['move_tasks', {}]]))).toEqual({
      kind: 'webOnly',
      detail: { action: 'move_tasks', where: 'Tasks move on the web task board' },
    });
    expect(failed(run(spec(), 'Create a Feature', [['create_feature', {}]]))).toEqual({
      kind: 'webOnly',
      detail: {
        action: 'create_feature',
        where: 'New Features: the web Backlog, or say it on the Backlog tab',
        page: { page: 'backlog', level: 'features' },
      },
    });
  });

  it('fails with unclear when there are no calls', () => {
    expect(failed(resolveCommand(spec(), [], aTranscript({ text: 'Um, the thing' })))).toEqual({
      kind: 'unclear',
      detail: { heard: 'Um, the thing' },
    });
    expect(failed(spec().toActions([], aTranscript({ text: 'Okay so' }))).kind).toBe('unclear');
  });

  it('keeps one line for each action, in order, with ids that do not depend on position', () => {
    const text = 'blocked on the artifact upload design';
    const r = done(
      run(
        spec(),
        `Set SHIP-892 status: ${text}`,
        [
          ['set_status', { feature: 'SHIP-892' }],
          ['set_health', { feature: 'SHIP-707', health: 'atRisk' }],
        ],
        text,
      ),
    );
    expect(r.lines).toHaveLength(r.actions.length);
    r.actions.forEach((a, i) => expect(r.lines[i].id).toContain(`:${a.itemId}:`));
    expect(new Set(r.lines.map((l) => l.id)).size).toBe(r.lines.length);
  });

  it('gives a partial preview when one call of two cannot be resolved', () => {
    const r = done(
      run(spec(), 'Mark SHIP-707 at risk and SHIP-999 blocked', [
        ['set_health', { feature: 'SHIP-707', health: 'atRisk' }],
        ['set_health', { feature: 'SHIP-999', health: 'blocked' }],
      ]),
    );
    expect(r.actions).toHaveLength(1);
    expect(r.rejected).toHaveLength(1);
  });

  it('builds a Feature row for a focus outside the visible rows', () => {
    const extra = aFeatureRow({ key: 'SHIP-5000', title: 'Elsewhere' });
    const s = spec({ focus: extra, rows: [extra], order: [extra], board: true });
    const r = done(run(s, 'Mark it blocked', [['set_health', { health: 'blocked' }]]));
    expect(r.actions[0]).toMatchObject({ key: 'SHIP-5000' });
  });
});

describe('features test set', () => {
  const rows = readFileSync(new URL('../voice/testset/features.jsonl', import.meta.url), 'utf8')
    .split('\n')
    .filter((l) => l !== '')
    .map((l) => JSON.parse(l) as TestSetRow);

  const KIND: Record<string, string> = {
    set_state: 'setState',
    add_note: 'addNote',
    set_status: 'setStatus',
    set_health: 'setHealth',
    answer: 'answer',
    approve: 'approve',
    move_rank: 'moveRank',
  };
  const WEB_ONLY = ['move_tasks', 'create_feature'];

  it('has at least 40 rows, three per action, four blocked statuses and four failures', () => {
    expect(rows.length).toBeGreaterThanOrEqual(40);
    for (const a of FEATURES_ACTIONS.filter((x) => x.webOnly === undefined))
      expect(
        rows.filter((r) => r.calls.some((c) => c.action === a.name)).length,
        a.name,
      ).toBeGreaterThanOrEqual(3);
    expect(
      rows.filter((r) => r.calls[0]?.action === 'set_status' && /\bblocked\b/i.test(r.note ?? '')),
    ).toHaveLength(4);
    expect(rows.filter((r) => r.calls.length === 0).length).toBeGreaterThanOrEqual(4);
    expect(rows.every((r) => r.screen === 'features')).toBe(true);
  });

  it('replays every test-set row through toActions to its expected calls and note text', () => {
    for (const r of rows) {
      const label = r.utterance;
      const result = run(
        spec(),
        r.utterance,
        r.calls.map((c) => [c.action, c.args]),
        r.note ?? undefined,
      );
      if (r.calls.length === 0) {
        expect(failed(result).kind, label).toBe('unclear');
        continue;
      }
      const [call] = r.calls;
      if (WEB_ONLY.includes(call.action)) {
        expect(failed(result).kind, label).toBe('webOnly');
        continue;
      }
      const ok = done(result);
      expect(ok.rejected, label).toEqual([]);
      const kind = KIND[call.action];
      const blocked =
        call.action === 'set_status' && /\bblocked\b/i.test(r.note ?? '') ? ['setHealth'] : [];
      expect(
        ok.actions.map((a) => a.kind),
        label,
      ).toEqual(kind === undefined ? [] : [kind, ...blocked]);
      if (r.note !== null) {
        const a = ok.actions[0];
        expect(
          a.kind === 'addNote'
            ? a.body
            : a.kind === 'setStatus'
              ? a.text
              : a.kind === 'answer'
                ? a.body
                : null,
          label,
        ).toBe(r.note);
      }
      expect(ok.lines.length, label).toBe(ok.actions.length);
    }
  });
});
