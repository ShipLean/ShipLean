import { describe, expect, it } from 'vitest';
import type { AppAction, PreviewLine } from '../actions/types';
import { aTranscript } from '../test-support/voice';
import type { ParsedCall, ScreenVoiceSpec } from './contracts';
import {
  copyNoteSpan,
  matchKey,
  matchPerson,
  matchState,
  resolveCommand,
  unmatched,
} from './resolve';

const keys = ['SHIP-906', 'SHIP-960', 'SHIP-1091'];
const people = [
  { id: 'u1', name: 'Priya Shah' },
  { id: 'u2', name: 'Tom Reed' },
  { id: 'u3', name: 'Tom Hall' },
  { id: 'u4', name: 'Mary-Ann Lee' },
];
const states = [
  { id: 's1', name: 'Active' },
  { id: 's2', name: 'In progress' },
];

describe('matchKey', () => {
  it('matches the exact key', () => {
    expect(matchKey('SHIP-906', keys)).toEqual({ kind: 'match', value: 'SHIP-906' });
  });
  it('matches whatever the case', () => {
    expect(matchKey('ship-1091', keys)).toEqual({ kind: 'match', value: 'SHIP-1091' });
  });
  it('matches with a space or no hyphen', () => {
    expect(matchKey('ship 906', keys)).toEqual({ kind: 'match', value: 'SHIP-906' });
    expect(matchKey('SHIP960', keys)).toEqual({ kind: 'match', value: 'SHIP-960' });
  });
  it('finds none for a key that isn’t there', () => {
    expect(matchKey('ship 907', keys)).toEqual({ kind: 'none' });
    expect(matchKey('', keys)).toEqual({ kind: 'none' });
  });
});

describe('matchPerson', () => {
  it('matches the exact full name', () => {
    expect(matchPerson('Tom Reed', people)).toEqual({ kind: 'match', value: people[1] });
  });
  it('matches whatever the case, and a first name only one person has', () => {
    expect(matchPerson('priya shah', people)).toEqual({ kind: 'match', value: people[0] });
    expect(matchPerson('PRIYA', people)).toEqual({ kind: 'match', value: people[0] });
  });
  it('matches with a hyphen for a space', () => {
    expect(matchPerson('tom-hall', people)).toEqual({ kind: 'match', value: people[2] });
  });
  it('matches a hyphenated first name whole, and not by its first part', () => {
    expect(matchPerson('Mary-Ann', people)).toEqual({ kind: 'match', value: people[3] });
    expect(matchPerson('mary ann', people)).toEqual({ kind: 'match', value: people[3] });
    expect(matchPerson('Mary', people)).toEqual({ kind: 'none' });
  });
  it('finds none for a shared first name or a stranger', () => {
    expect(matchPerson('Tom', people)).toEqual({ kind: 'none' });
    expect(matchPerson('Ana', people)).toEqual({ kind: 'none' });
  });
});

describe('matchState', () => {
  it('matches the exact state', () => {
    expect(matchState('Active', states)).toEqual({ kind: 'match', value: states[0] });
  });
  it('matches whatever the case', () => {
    expect(matchState('active', states)).toEqual({ kind: 'match', value: states[0] });
  });
  it('matches with a hyphen or no space', () => {
    expect(matchState('in-progress', states)).toEqual({ kind: 'match', value: states[1] });
    expect(matchState('inprogress', states)).toEqual({ kind: 'match', value: states[1] });
  });
  it('finds none for a state the type doesn’t have', () => {
    expect(matchState('Done', states)).toEqual({ kind: 'none' });
  });
});

describe('copyNoteSpan', () => {
  it('copyNoteSpan copies the exact substring and refuses a span outside the transcript', () => {
    const t = aTranscript({ text: 'add a note: the API returns 500 on retry' });
    expect(copyNoteSpan(t, { start: 12, end: 40 })).toBe('the API returns 500 on retry');
    expect(copyNoteSpan(t, { start: 0, end: 3 })).toBe('add');
    expect(copyNoteSpan(t, { start: 12, end: 41 })).toBeNull();
    expect(copyNoteSpan(t, { start: -1, end: 3 })).toBeNull();
    expect(copyNoteSpan(t, { start: 5, end: 5 })).toBeNull();
  });
});

describe('unmatched', () => {
  it('turns none into noMatch and misheard into misheardKey', () => {
    expect(unmatched('ship 907', 'target item', { kind: 'none' })).toEqual({
      kind: 'noMatch',
      detail: { spoken: 'ship 907', role: 'target item', span: null },
    });
    expect(
      unmatched('ship 960', 'target item', {
        kind: 'misheard',
        heard: 'SHIP-960',
        suggestion: 'SHIP-906',
      }),
    ).toEqual({ kind: 'misheardKey', detail: { spoken: 'ship 960', suggestion: 'SHIP-906' } });
  });
});

describe('matchKey (loose)', () => {
  it('returns misheard with SHIP-906 for "SHIP-960" when SHIP-906 is a candidate, and none otherwise', () => {
    expect(matchKey('SHIP-960', ['SHIP-906', 'SHIP-1091'])).toEqual({
      kind: 'misheard',
      heard: 'SHIP-960',
      suggestion: 'SHIP-906',
    });
    expect(matchKey('SHIP-960', ['SHIP-1091'])).toEqual({ kind: 'none' });
  });
  it('matches a key said as words', () => {
    expect(matchKey('ship nine oh six', keys)).toEqual({ kind: 'match', value: 'SHIP-906' });
    expect(matchKey('ship nine sixty', ['SHIP-906'])).toEqual({
      kind: 'misheard',
      heard: 'SHIP-960',
      suggestion: 'SHIP-906',
    });
  });
});

describe('matchPerson (loose)', () => {
  it('matches a surname, an accent-free spelling and a small slip, and never a shared first name', () => {
    const team = [...people, { id: 'u5', name: 'Zoë Ng' }, { id: 'u6', name: 'Priya Rao' }];
    expect(matchPerson('Shah', team)).toEqual({ kind: 'match', value: people[0] });
    expect(matchPerson('zoe', team)).toEqual({ kind: 'match', value: team[4] });
    expect(matchPerson('Priya Sha', team)).toEqual({ kind: 'match', value: people[0] });
    expect(matchPerson('Priya', team)).toEqual({ kind: 'none' });
  });
});

describe('matchState (loose)', () => {
  it('matches "in progress", "In-Progress" and "resolve" to the type’s states', () => {
    const bugStates = [
      { id: 'b1', name: 'New' },
      { id: 'b2', name: 'In Progress' },
      { id: 'b3', name: 'Resolved' },
    ];
    expect(matchState('in progress', bugStates)).toEqual({ kind: 'match', value: bugStates[1] });
    expect(matchState('In-Progress', bugStates)).toEqual({ kind: 'match', value: bugStates[1] });
    expect(matchState('resolve', bugStates)).toEqual({ kind: 'match', value: bugStates[2] });
    expect(matchState('Done', bugStates)).toEqual({ kind: 'none' });
  });
});

describe('copyNoteSpan (trimming)', () => {
  it('takes off the spaces around the note and a spoken leading "colon" or "that"', () => {
    const t = aTranscript({ text: 'note colon the build is red' });
    expect(copyNoteSpan(t, { start: 4, end: 27 })).toBe('the build is red');
    const u = aTranscript({ text: 'comment that retries double the load' });
    expect(copyNoteSpan(u, { start: 7, end: 36 })).toBe('retries double the load');
  });
  it('refuses a span that cuts a word in half', () => {
    const t = aTranscript({ text: 'add a note: the API returns 500 on retry' });
    expect(copyNoteSpan(t, { start: 13, end: 40 })).toBeNull();
    expect(copyNoteSpan(t, { start: 12, end: 39 })).toBeNull();
  });
});

// A tiny Feature screen spec that acts on a match only and hands anything else to unmatched, as the screens do.
type FeatureAction = AppAction & { value: string };
const featureStates = [
  { id: 'f1', name: 'Proposed' },
  { id: 'f2', name: 'Active' },
  { id: 'f3', name: 'Resolved' },
  { id: 'f4', name: 'Closed' },
];
const team = [
  { id: 'u1', name: 'Priya Shah' },
  { id: 'u2', name: 'Priya Rao' },
  { id: 'u3', name: 'Tom Reed' },
];
const onScreen = ['SHIP-906', 'SHIP-1091'];

function featureSpec(seen: ParsedCall[][] = []): ScreenVoiceSpec<FeatureAction> {
  const arg = (c: ParsedCall, name: string) => String(c.args[name] ?? '');
  return {
    screen: 'featureBoard',
    actions: [
      {
        name: 'setState',
        description: 'Move a Feature to a state',
        args: {
          type: 'object',
          properties: { item: { type: 'string' }, state: { type: 'string' } },
          required: ['state'],
        },
      },
      {
        name: 'assign',
        description: 'Give a Feature to someone',
        args: {
          type: 'object',
          properties: { item: { type: 'string' }, person: { type: 'string' } },
          required: ['item', 'person'],
        },
      },
      {
        name: 'comment',
        description: 'Add a note',
        args: {
          type: 'object',
          properties: { item: { type: 'string' }, text: { type: 'string' } },
          required: ['item', 'text'],
        },
        noteArg: 'text',
      },
      {
        name: 'split',
        description: 'Split a Feature',
        args: { type: 'object', properties: { item: { type: 'string' } } },
        webOnly: 'the Feature page on the web',
      },
    ],
    context: () => ({ focus: 'SHIP-906', keys: onScreen, states: featureStates, people: team }),
    hotwords: () => onScreen,
    toActions(calls: ParsedCall[]) {
      seen.push(calls);
      const actions: FeatureAction[] = [];
      const lines: PreviewLine[] = [];
      for (const c of calls) {
        const spoken = c.args.item === undefined ? 'SHIP-906' : arg(c, 'item');
        const key = matchKey(spoken, onScreen);
        if (key.kind !== 'match') return { ok: false, failure: unmatched(spoken, 'item', key) };
        let value: string;
        if (c.action === 'setState') {
          const s = matchState(arg(c, 'state'), featureStates);
          if (s.kind !== 'match')
            return { ok: false, failure: unmatched(arg(c, 'state'), 'state', s) };
          value = s.value.name;
        } else if (c.action === 'assign') {
          const p = matchPerson(arg(c, 'person'), team);
          if (p.kind !== 'match')
            return { ok: false, failure: unmatched(arg(c, 'person'), 'person', p) };
          value = p.value.name;
        } else value = arg(c, 'text');
        actions.push({ area: 'features', kind: c.action, itemId: null, key: key.value, value });
        lines.push({
          id: `${c.action}:${key.value}`,
          icon: c.action,
          text: value,
          removable: true,
        });
      }
      return { ok: true, actions, lines, rejected: [], view: null };
    },
  };
}

describe('resolveCommand', () => {
  it('a missing key that sounds like a visible key fails as misheardKey with one suggestion and the words heard', () => {
    const t = aTranscript({ text: 'set SHIP-960 to active' });
    const calls = [{ action: 'setState', args: { item: 'SHIP-960', state: 'active' } }];
    expect(resolveCommand(featureSpec(), calls, t)).toEqual({
      ok: false,
      failure: { kind: 'misheardKey', detail: { spoken: 'SHIP-960', suggestion: 'SHIP-906' } },
    });
  });

  it('an item phrase that matches nothing fails as noMatch with the span and role item', () => {
    const t = aTranscript({ text: 'assign SHIP-4321 to Tom' });
    const calls = [{ action: 'assign', args: { item: 'SHIP-4321', person: 'Tom' } }];
    expect(resolveCommand(featureSpec(), calls, t)).toEqual({
      ok: false,
      failure: {
        kind: 'noMatch',
        detail: { spoken: 'SHIP-4321', role: 'item', span: { start: 7, end: 16 } },
      },
    });
  });

  it('"set this to Done" on a Feature fails as noValidAction with reason noSuchState', () => {
    const spec = featureSpec();
    const t = aTranscript({ text: 'set this to Done' });
    expect(resolveCommand(spec, [{ action: 'setState', args: { state: 'Done' } }], t)).toEqual({
      ok: false,
      failure: { kind: 'noValidAction', detail: { action: 'setState', reason: 'noSuchState' } },
    });
    // The sheet's chips are the Feature type's states, read from the spec's context.
    expect(spec.context().states).toEqual(featureStates);
  });

  it('a webOnly action is rejected with its where; with another valid call it resolves ok with one rejected line', () => {
    const t = aTranscript({ text: 'split this and set it to active' });
    const split = { action: 'split', args: {} };
    const webOnly = {
      kind: 'webOnly',
      detail: { action: 'split', where: 'the Feature page on the web' },
    };
    expect(resolveCommand(featureSpec(), [split], t)).toEqual({ ok: false, failure: webOnly });

    const seen: ParsedCall[][] = [];
    const both = resolveCommand(
      featureSpec(seen),
      [split, { action: 'setState', args: { state: 'active' } }],
      t,
    );
    expect(both).toEqual({
      ok: true,
      actions: [
        { area: 'features', kind: 'setState', itemId: null, key: 'SHIP-906', value: 'Active' },
      ],
      lines: [{ id: 'setState:SHIP-906', icon: 'setState', text: 'Active', removable: true }],
      rejected: [{ call: split, failure: webOnly }],
      view: null,
    });
    // The spec never sees the web-only call.
    expect(seen).toEqual([[{ action: 'setState', args: { state: 'active' } }]]);
  });

  it('no calls fails as unclear and keeps the heard text', () => {
    const t = aTranscript({ text: 'hmm what was that about the thing' });
    expect(resolveCommand(featureSpec(), [], t)).toEqual({
      ok: false,
      failure: { kind: 'unclear', detail: { heard: 'hmm what was that about the thing' } },
    });
  });

  it('two people called Priya fail as noMatch with role person', () => {
    const t = aTranscript({ text: 'give this to Priya' });
    expect(
      resolveCommand(
        featureSpec(),
        [{ action: 'assign', args: { item: 'SHIP-906', person: 'Priya' } }],
        t,
      ),
    ).toEqual({
      ok: false,
      failure: {
        kind: 'noMatch',
        detail: { spoken: 'Priya', role: 'person', span: { start: 13, end: 18 } },
      },
    });
  });

  it('returns the transcript’s own words even when the model’s args hold a paraphrase', () => {
    const t = aTranscript({ text: 'comment on this colon the API returns 500 on retry' });
    const call = {
      action: 'comment',
      args: { item: 'SHIP-906', text: 'API fails with a server error when retried' },
      noteSpan: { start: 16, end: 50 },
    };
    const result = resolveCommand(featureSpec(), [call], t);
    if (!result.ok) throw new Error(`expected ok, got ${result.failure.kind}`);
    expect(result.actions.map((a) => a.value)).toEqual(['the API returns 500 on retry']);
  });

  it('a span cutting a word fails as unclear', () => {
    const t = aTranscript({ text: 'comment on this colon the API returns 500 on retry' });
    const call = {
      action: 'comment',
      args: { item: 'SHIP-906' },
      noteSpan: { start: 22, end: 46 },
    };
    expect(resolveCommand(featureSpec(), [call], t)).toEqual({
      ok: false,
      failure: { kind: 'unclear', detail: { heard: t.text } },
    });
    expect(
      resolveCommand(featureSpec(), [{ action: 'comment', args: { item: 'SHIP-906' } }], t),
    ).toEqual({ ok: false, failure: { kind: 'unclear', detail: { heard: t.text } } });
  });

  it('keeps the calls that resolve when another names an item that isn’t there, each line with its action', () => {
    const t = aTranscript({
      text: 'resolve this, assign SHIP-4321 to Tom and give SHIP-1091 to Tom',
    });
    const missing = { action: 'assign', args: { item: 'SHIP-4321', person: 'Tom' } };
    const result = resolveCommand(
      featureSpec(),
      [
        { action: 'setState', args: { state: 'resolve' } },
        missing,
        { action: 'assign', args: { item: 'SHIP-1091', person: 'Tom' } },
      ],
      t,
    );
    if (!result.ok) throw new Error(`expected ok, got ${result.failure.kind}`);
    expect(result.actions.map((a) => `${a.key} ${a.value}`)).toEqual([
      'SHIP-906 Resolved',
      'SHIP-1091 Tom Reed',
    ]);
    expect(result.lines.map((l) => l.id)).toEqual(['setState:SHIP-906', 'assign:SHIP-1091']);
    expect(result.rejected).toEqual([
      {
        call: missing,
        failure: {
          kind: 'noMatch',
          detail: { spoken: 'SHIP-4321', role: 'item', span: { start: 21, end: 30 } },
        },
      },
    ]);
  });

  it('fails with the first rejection when no call can be used', () => {
    const t = aTranscript({ text: 'delete this and split it' });
    expect(
      resolveCommand(
        featureSpec(),
        [
          { action: 'delete', args: {} },
          { action: 'split', args: {} },
        ],
        t,
      ),
    ).toEqual({
      ok: false,
      failure: { kind: 'noValidAction', detail: { action: 'delete', reason: 'none' } },
    });
  });
});
