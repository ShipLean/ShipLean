import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import type { MemberDto, StateDto } from '@shiplean/core/api/projects';
import members from '../../../ShipLean.TestSupport/DtoFixtures/projects/members.json';
import { aTranscript } from '../test-support/voice';
import {
  aDecision,
  aFeatureItem,
  anItemAsks,
  aQuestion,
  aStoryItem,
} from '../test-support/workItem';
import type { ParsedCall, ResolveResult, TestSetRow, VoiceFailure } from '../voice/contracts';
import { resolveCommand } from '../voice/resolve';
import { links } from '../links';
import type { WorkItemAction } from './actions';
import { WORK_ITEM_ACTIONS, workItemVoiceSpec, type WorkItemVoiceContext } from './voiceSpec';

// states.json holds no Feature states, so the Feature's own are set here (the item is in Active).
const state = (type: StateDto['type'], name: string, category: StateDto['category'], n: number) =>
  ({
    id: `00000003-0000-4000-8001-0000000${type === 'feature' ? '02' : '03'}0${n}`,
    type,
    name,
    category,
    order: n,
    itemCount: 0,
  }) as StateDto;

const STATES: StateDto[] = [
  state('feature', 'New', 'proposed', 1),
  state('feature', 'Active', 'inProgress', 2),
  state('feature', 'Done', 'completed', 3),
  state('story', 'Triaged', 'proposed', 1),
  state('story', 'Active', 'inProgress', 2),
];

const asDefault = () => {
  const item = aFeatureItem();
  return {
    item,
    asks: anItemAsks(item.id, {
      decisions: [
        aDecision(),
        aDecision({ number: 2, label: 'D2', question: 'Which model runs on a phone with 4 GB?' }),
      ],
      questions: [aQuestion()],
    }),
  };
};

function context(patch: Partial<WorkItemVoiceContext> = {}): WorkItemVoiceContext {
  return { ...asDefault(), states: STATES, members: members as MemberDto[], ...patch };
}

function run(
  ctx: WorkItemVoiceContext,
  utterance: string,
  calls: [string, Record<string, unknown>][],
  note?: string,
): ResolveResult<WorkItemAction> {
  const span =
    note === undefined
      ? undefined
      : { start: utterance.indexOf(note), end: utterance.indexOf(note) + note.length };
  const parsed: ParsedCall[] = calls.map(([action, args]) => ({
    action,
    args,
    ...(span !== undefined &&
    WORK_ITEM_ACTIONS.find((a) => a.name === action)?.noteArg !== undefined
      ? { noteSpan: span }
      : {}),
  }));
  return resolveCommand(workItemVoiceSpec(ctx), parsed, aTranscript({ text: utterance }));
}

function done(result: ResolveResult<WorkItemAction>) {
  if (!result.ok) throw new Error(`failed: ${result.failure.kind}`);
  return result;
}
const failed = (result: ResolveResult<WorkItemAction>): VoiceFailure => {
  if (result.ok) throw new Error('expected a failure');
  return result.failure;
};
const only = (result: ResolveResult<WorkItemAction>) => {
  const r = done(result);
  expect(r.actions).toHaveLength(1);
  expect(r.lines).toHaveLength(1);
  return r.actions[0];
};

describe('work item voice spec', () => {
  it('declares eleven actions plus two web-only ones with short descriptions', () => {
    const spec = workItemVoiceSpec(context());
    expect(spec.actions.filter((a) => a.webOnly === undefined).map((a) => a.name)).toEqual([
      'add_note',
      'set_state',
      'approve_decision',
      'discuss_decision',
      'reply',
      'confirm_status',
      'set_status',
      'set_health',
      'assign',
      'ask',
      'open',
    ]);
    expect(spec.actions.filter((a) => a.webOnly !== undefined).map((a) => a.name)).toEqual([
      'edit_fields',
      'move_tasks',
    ]);
    for (const a of spec.actions) expect(a.description.length).toBeLessThanOrEqual(110);
  });

  it('add_note copies the note word for word', () => {
    const a = only(
      run(
        context(),
        'Add a note: Parakeet needs 1.4 GB, on the S22!',
        [['add_note', { note: 'x' }]],
        'Parakeet needs 1.4 GB, on the S22!',
      ),
    );
    expect(a).toMatchObject({
      kind: 'addNote',
      key: 'SHIP-906',
      body: 'Parakeet needs 1.4 GB, on the S22!',
    });
  });

  it('add_note fails with unclear when the span is not in the transcript', () => {
    const r = resolveCommand(
      workItemVoiceSpec(context()),
      [{ action: 'add_note', args: { note: 'x' }, noteSpan: { start: 0, end: 500 } }],
      aTranscript({ text: 'Add a note' }),
    );
    expect(failed(r).kind).toBe('unclear');
  });

  it('set_state matches a state of the item type', () => {
    const a = only(run(context(), 'Move this to done', [['set_state', { state: 'Done' }]]));
    expect(a).toMatchObject({
      kind: 'setState',
      stateName: 'Done',
      fromStateName: 'Active',
      revision: aFeatureItem().revision,
    });
    // Triaged is a Story state, not a Feature one.
    expect(
      failed(run(context(), 'Move this to triaged', [['set_state', { state: 'Triaged' }]])),
    ).toEqual({
      kind: 'noValidAction',
      detail: { action: 'set_state', reason: 'noSuchState' },
    });
  });

  it('approve_decision approves open decisions only', () => {
    const a = only(
      run(context(), 'Approve decisions 1 and 2', [['approve_decision', { decisions: [1, 2] }]]),
    );
    expect(a).toMatchObject({ kind: 'approve', numbers: [1, 2] });
    const ctx = context();
    ctx.asks = anItemAsks(ctx.item.id, {
      decisions: [aDecision(), aDecision({ number: 2, label: 'D2', state: 'confirmed' })],
    });
    expect(failed(run(ctx, 'Approve D2', [['approve_decision', { decisions: [2] }]])).kind).toBe(
      'ambiguous',
    );
  });

  it('discuss_decision posts the note on an open decision', () => {
    const r = done(
      run(
        context(),
        'Discuss D2: the 4 GB number looks too low',
        [['discuss_decision', { decision: 2, note: 'x' }]],
        'the 4 GB number looks too low',
      ),
    );
    expect(r.actions[0]).toMatchObject({
      kind: 'discuss',
      number: 2,
      body: 'the 4 GB number looks too low',
    });
    expect(r.lines[0].detail).toBe('Posted on SHIP-906. The next agent to pick it up reads it.');
  });

  it('reply answers the only open question when none is named', () => {
    const a = only(
      run(
        context(),
        'Reply yes, use the small model',
        [['reply', { note: 'x' }]],
        'yes, use the small model',
      ),
    );
    expect(a).toMatchObject({ kind: 'reply', number: 1, body: 'yes, use the small model' });
  });

  it('confirm_status confirms a Feature status line', () => {
    expect(only(run(context(), 'Confirm the status', [['confirm_status', {}]]))).toMatchObject({
      kind: 'confirmStatus',
    });
  });

  it('set_status sets the status line and keeps the old one for Undo', () => {
    const a = only(
      run(
        context(),
        'Update the status: shipped on 12 Nov',
        [['set_status', { note: 'x' }]],
        'shipped on 12 Nov',
      ),
    );
    expect(a).toMatchObject({
      kind: 'setStatus',
      text: 'shipped on 12 Nov',
      from: aFeatureItem().statusLine,
    });
  });

  it('set_status refuses a line over 500 characters', () => {
    const long = `${'word '.repeat(110)}end`;
    const r = run(context(), `Status: ${long}`, [['set_status', { note: 'x' }]], long);
    expect(failed(r)).toEqual({
      kind: 'noValidAction',
      detail: { action: 'set_status', reason: 'invalidArgs' },
    });
  });

  it('proposes health Blocked as its own line for a blocked status on a Feature', () => {
    const r = done(
      run(
        context(),
        'Status: Blocked on the Play Store review',
        [['set_status', { note: 'x' }]],
        'Blocked on the Play Store review',
      ),
    );
    expect(r.actions.map((a) => a.kind)).toEqual(['setStatus', 'setHealth']);
    expect(r.actions[1]).toMatchObject({ kind: 'setHealth', health: 'blocked', from: 'atRisk' });
    expect(r.lines).toHaveLength(2);
    expect(r.lines.every((l) => l.removable)).toBe(true);
    expect(r.lines[1].text).toBe('SHIP-906 health: At risk → Blocked');
    // Already blocked: the status alone.
    const ctx = context();
    ctx.item = { ...ctx.item, health: 'blocked' };
    expect(
      done(
        run(ctx, 'Status: blocked on review', [['set_status', { note: 'x' }]], 'blocked on review'),
      ).actions,
    ).toHaveLength(1);
  });

  it('set_health sets the health on a Feature only', () => {
    expect(
      only(run(context(), 'Mark this at risk', [['set_health', { health: 'onTrack' }]])),
    ).toMatchObject({
      kind: 'setHealth',
      health: 'onTrack',
      from: 'atRisk',
    });
    const story = context({ item: aStoryItem() });
    expect(failed(run(story, 'Mark this at risk', [['set_health', { health: 'atRisk' }]]))).toEqual(
      {
        kind: 'noValidAction',
        detail: { action: 'set_health', reason: 'notAllowed' },
      },
    );
  });

  it('assign matches a person of the project', () => {
    expect(
      only(run(context(), 'Assign this to Joel', [['assign', { person: 'Joel' }]])),
    ).toMatchObject({
      kind: 'assign',
      name: 'Joel Dickson',
      fromName: 'Priya Shah',
    });
    expect(
      failed(run(context(), 'Assign this to Zed Nobody', [['assign', { person: 'Zed Nobody' }]]))
        .kind,
    ).toBe('noMatch');
  });

  it('ask answers on screen and changes nothing', () => {
    const r = done(run(context(), "What's left?", [['ask', { topic: 'whatsLeft' }]]));
    expect(r.actions).toEqual([]);
    expect(r.lines).toEqual([]);
    expect(r.view).toMatchObject({ kind: 'answer', content: { rows: [{ key: 'SHIP-141' }] } });
    const needs = done(run(context(), 'What needs me?', [['ask', { topic: 'whatNeedsMe' }]]));
    expect(needs.view).toMatchObject({ content: { lead: '3 waiting on you' } });
  });

  it('open resolves a child key', () => {
    const r = done(run(context(), 'Open SHIP-141', [['open', { key: 'SHIP-141' }]]));
    expect(r.actions).toEqual([]);
    expect(r.view).toEqual({ kind: 'navigate', link: links.item('SHIP', 'SHIP-141') });
  });

  it('open resolves the parent and a linked item', () => {
    const story = aStoryItem({
      parent: {
        id: 'p',
        key: 'SHIP-906',
        type: { id: 'feature', name: 'Feature' },
        title: 'Voice',
      },
      links: [
        {
          id: 'l',
          type: 'related',
          workItemId: 'x',
          key: 'SHIP-55',
          title: 'Other',
          url: null,
          card: null,
          autoLinked: null,
        },
      ],
    });
    const ctx = context({ item: story, asks: null });
    expect(done(run(ctx, 'Open SHIP-906', [['open', { key: 'SHIP-906' }]])).view).toMatchObject({
      kind: 'navigate',
    });
    expect(done(run(ctx, 'Open SHIP-55', [['open', { key: 'SHIP-55' }]])).view).toEqual({
      kind: 'navigate',
      link: links.item('SHIP', 'SHIP-55'),
    });
    expect(failed(run(ctx, 'Open SHIP-777', [['open', { key: 'SHIP-777' }]])).kind).toBe('noMatch');
  });

  it('resolves the listening state: Active, approve D10 and a note in one utterance, in order', () => {
    const ctx = context();
    ctx.asks = anItemAsks(ctx.item.id, { decisions: [aDecision({ number: 10, label: 'D10' })] });
    const text = 'Set it to active, approve D10 and note that the model is cached';
    const r = done(
      run(
        ctx,
        text,
        [
          ['set_state', { state: 'Active' }],
          ['approve_decision', { decisions: [10] }],
          ['add_note', { note: 'x' }],
        ],
        'the model is cached',
      ),
    );
    expect(r.actions.map((a) => a.kind)).toEqual(['setState', 'approve', 'addNote']);
    expect(r.lines.map((l) => l.text)).toEqual([
      'State: Active → Active',
      'Approve D10',
      'Note on SHIP-906: "the model is cached"',
    ]);
    expect(r.lines.every((l) => l.removable)).toBe(true);
  });

  it('resolves the reply state: the reply quoted word for word with the posted-on detail', () => {
    const r = done(
      run(
        context(),
        'Reply: Yes, use the small model, but only on Wi-Fi.',
        [['reply', { note: 'x' }]],
        'Yes, use the small model, but only on Wi-Fi.',
      ),
    );
    expect(r.lines[0]).toMatchObject({
      text: 'Reply to Q1',
      detail: 'Posted on SHIP-906. The next agent to pick it up reads it.',
      removable: true,
    });
    expect(r.actions[0]).toMatchObject({ body: 'Yes, use the small model, but only on Wi-Fi.' });
  });

  it('fails with ambiguous for two open questions and no number', () => {
    const ctx = context();
    ctx.asks = anItemAsks(ctx.item.id, {
      questions: [
        aQuestion(),
        aQuestion({ number: 2, label: 'Q2', text: 'Ship on Android first?' }),
      ],
    });
    const f = failed(run(ctx, 'Reply yes', [['reply', { note: 'x' }]], 'yes'));
    expect(f.kind).toBe('ambiguous');
    if (f.kind === 'ambiguous') expect(f.detail.candidates.map((c) => c.key)).toEqual(['Q1', 'Q2']);
  });

  it('fails with noValidAction for confirm_status on a Story', () => {
    const story = context({ item: aStoryItem(), asks: null });
    expect(failed(run(story, 'Confirm the status', [['confirm_status', {}]]))).toEqual({
      kind: 'noValidAction',
      detail: { action: 'confirm_status', reason: 'notAllowed' },
    });
  });

  it('returns the keys the correction sheets read', () => {
    const c = workItemVoiceSpec(context()).context();
    expect(c.key).toBe('SHIP-906');
    expect(c.states).toEqual(['New', 'Active', 'Done']);
    expect(c.rows).toEqual([{ key: 'SHIP-141', title: 'Pay with saved card' }]);
  });

  it('hotwords name the keys, states, people and numbered asks', () => {
    const words = workItemVoiceSpec(context()).hotwords();
    for (const w of ['SHIP-906', 'SHIP-141', 'Active', 'Joel Dickson', 'D1', 'D2', 'Q1'])
      expect(words).toContain(w);
  });

  it('webOnly actions fail with the web-only sheet', () => {
    expect(failed(run(context(), 'Change the title', [['edit_fields', {}]])).kind).toBe('webOnly');
  });

  describe('test set', () => {
    const rows = readFileSync(new URL('../voice/testset/workItem.jsonl', import.meta.url), 'utf8')
      .split('\n')
      .filter((l) => l !== '')
      .map((l) => JSON.parse(l) as TestSetRow);
    const KIND: Record<string, string> = {
      add_note: 'addNote',
      set_state: 'setState',
      approve_decision: 'approve',
      discuss_decision: 'discuss',
      reply: 'reply',
      confirm_status: 'confirmStatus',
      set_status: 'setStatus',
      set_health: 'setHealth',
      assign: 'assign',
    };
    const FAILS: Record<string, string> = {
      'Approve D5': 'ambiguous',
      'Discuss D7: the number is wrong': 'ambiguous',
      'Reply to question 4: yes please': 'ambiguous',
      'Approve D1 and D9': 'ambiguous',
      'Move this to Triaged': 'noValidAction',
      'Set it to Cancelled': 'noValidAction',
      'Mark it as flying': 'noValidAction',
      'Put it in the review column': 'noValidAction',
      'Change the title to something shorter': 'webOnly',
      'Move the tasks to Done': 'webOnly',
    };

    it('has at least 40 rows, three per action, six multi-action and four of each structural failure', () => {
      expect(rows.length).toBeGreaterThanOrEqual(40);
      for (const a of WORK_ITEM_ACTIONS.filter((x) => x.webOnly === undefined))
        expect(
          rows.filter((r) => r.calls.some((c) => c.action === a.name)).length,
          a.name,
        ).toBeGreaterThanOrEqual(3);
      expect(rows.filter((r) => r.calls.length > 1).length).toBeGreaterThanOrEqual(6);
      const kinds = Object.values(FAILS);
      expect(kinds.filter((k) => k === 'ambiguous')).toHaveLength(4);
      expect(kinds.filter((k) => k === 'noValidAction')).toHaveLength(4);
      expect(rows.filter((r) => r.calls.length === 0)).toHaveLength(4);
    });

    it('replays every test-set row through toActions to its expected calls and note text', () => {
      for (const row of rows) {
        const label = row.utterance;
        const result = run(
          context(),
          label,
          row.calls.map((c) => [c.action, c.args]),
          row.note ?? undefined,
        );
        if (row.calls.length === 0) {
          expect(failed(result).kind, label).toBe('unclear');
          continue;
        }
        if (FAILS[label] !== undefined) {
          expect(failed(result).kind, label).toBe(FAILS[label]);
          continue;
        }
        const r = done(result);
        expect(r.rejected, label).toEqual([]);
        const expected = row.calls.flatMap((c) => {
          const kind = KIND[c.action];
          if (kind === undefined) return [];
          return c.action === 'set_status' && /blocked/i.test(row.note ?? '')
            ? [kind, 'setHealth']
            : [kind];
        });
        expect(
          r.actions.map((a) => a.kind),
          label,
        ).toEqual(expected);
        expect(r.lines.length, label).toBe(r.actions.length);
        if (row.note !== null) {
          const noted = r.actions.flatMap((a) =>
            a.kind === 'addNote' || a.kind === 'discuss' || a.kind === 'reply'
              ? [a.body]
              : a.kind === 'setStatus'
                ? [a.text]
                : [],
          );
          expect(noted, label).toEqual([row.note]);
        }
        const viewCalls = row.calls.filter((c) => c.action === 'ask' || c.action === 'open');
        expect(r.view?.kind ?? null, label).toBe(
          viewCalls.length === 0 ? null : viewCalls[0].action === 'ask' ? 'answer' : 'navigate',
        );
      }
    });
  });
});
