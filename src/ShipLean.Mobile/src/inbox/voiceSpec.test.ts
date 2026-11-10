import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import type { InboxItemDto } from '@shiplean/core/api/inbox';
import { designInbox } from '../test-support/inboxDesign';
import { aTranscript } from '../test-support/voice';
import type { ParsedCall, ResolveResult, TestSetRow, VoiceFailure } from '../voice/contracts';
import { resolveCommand } from '../voice/resolve';
import type { InboxAction } from './actions';
import { INBOX_ACTIONS, inboxVoiceSpec } from './voiceSpec';

const inbox = designInbox();
const spec = inboxVoiceSpec(inbox);
const item = (key: string): InboxItemDto => inbox.items.find((i) => i.key === key)!;

// Runs the calls through resolveCommand, as the phone does. A note is the span of `note` in the utterance.
function run(
  utterance: string,
  calls: [string, Record<string, unknown>][],
  note?: string,
): ResolveResult<InboxAction> {
  const span =
    note === undefined
      ? undefined
      : { start: utterance.indexOf(note), end: utterance.indexOf(note) + note.length };
  const parsed: ParsedCall[] = calls.map(([action, args]) => ({
    action,
    args,
    ...(span !== undefined && INBOX_ACTIONS.find((a) => a.name === action)?.noteArg !== undefined
      ? { noteSpan: span }
      : {}),
  }));
  return resolveCommand(spec, parsed, aTranscript({ text: utterance }));
}

function done(result: ResolveResult<InboxAction>) {
  if (!result.ok) throw new Error(`failed: ${result.failure.kind}`);
  return result;
}

function failed(result: ResolveResult<InboxAction>): VoiceFailure {
  if (result.ok) throw new Error('expected a failure');
  return result.failure;
}

const base = (key: string) => ({
  area: 'inbox',
  projectId: inbox.projectId,
  itemId: item(key).id,
  key,
});

describe('inboxVoiceSpec', () => {
  it('approves D10 on SHIP-906 as one approve action', () => {
    const r = done(
      run('Approve D10 on SHIP-906', [['approve', { key: 'SHIP-906', decisions: [10] }]]),
    );
    expect(r.actions).toEqual([{ ...base('SHIP-906'), kind: 'approve', numbers: [10] }]);
    expect(r.lines).toHaveLength(1);
    expect(r.lines[0].warning).toBeUndefined();
  });

  it('approves D1 to D8 on SHIP-907 with a "haven\'t opened them yet" warning', () => {
    const r = done(
      run('Confirm D1 to D8 on SHIP-907', [
        ['approve', { key: 'SHIP-907', decisions: [1, 2, 3, 4, 5, 6, 7, 8] }],
      ]),
    );
    expect(r.actions).toEqual([
      { ...base('SHIP-907'), kind: 'approve', numbers: [1, 2, 3, 4, 5, 6, 7, 8] },
    ]);
    expect(r.lines[0]).toMatchObject({
      text: 'Confirm D1–D8 on SHIP-907',
      detail: 'All 8 recommended answers',
      warning: "you haven't opened them yet",
      removable: true,
    });
  });

  it('chooses the alternative for D4 on SHIP-907', () => {
    const r = done(
      run('Change D4 on SHIP-907 to the app’s key-value store', [
        ['chooseDecision', { key: 'SHIP-907', label: 'D4', option: 'The app’s key-value store' }],
      ]),
    );
    expect(r.actions).toEqual([
      { ...base('SHIP-907'), kind: 'chooseDecision', number: 4, alternativeIndex: 0 },
    ]);
    const recommended = done(
      run('Take the recommended D4 on SHIP-907', [
        ['chooseDecision', { key: 'SHIP-907', label: 'D4' }],
      ]),
    );
    expect(recommended.actions[0]).toMatchObject({
      kind: 'chooseDecision',
      alternativeIndex: null,
    });
  });

  it('chooses "Personal team" on SHIP-924 as chooseOption', () => {
    const r = done(
      run('Pick personal team for Q1 on SHIP-924', [
        ['chooseDecision', { key: 'SHIP-924', label: 'Q1', option: 'Personal team' }],
      ]),
    );
    expect(r.actions).toEqual([
      {
        ...base('SHIP-924'),
        kind: 'chooseOption',
        number: 1,
        optionIndex: 0,
        option: 'Personal team',
      },
    ]);
  });

  it('answers Priya Shah on SHIP-892 with the words copied exactly', () => {
    const words = 'FIFTY MB,  and trim anything longer than two minutes';
    const r = done(
      run(
        `Answer Priya Shah: ${words}`,
        [['answer', { person: 'Priya Shah', note: 'rewritten' }]],
        words,
      ),
    );
    expect(r.actions).toEqual([{ ...base('SHIP-892'), kind: 'answer', number: 7, body: words }]);
    expect(r.lines[0].text).toBe('Answer Priya Shah on SHIP-892');
    expect(r.lines[0].detail).toContain(`“${words}”`);
    expect(r.lines[0].detail).toContain(
      'Posted on SHIP-892. The next agent to pick it up reads it.',
    );
    expect(JSON.stringify(r.lines)).not.toMatch(/notified|waiting/i);
  });

  it('discusses D11 on SHIP-906 and keeps the decision open', () => {
    const words = 'the dock covers the list on small phones.';
    const r = done(
      run(
        `Discuss D11 on SHIP-906: ${words}`,
        [['discuss', { key: 'SHIP-906', decision: 11, note: 'x' }]],
        words,
      ),
    );
    expect(r.actions).toEqual([{ ...base('SHIP-906'), kind: 'discuss', number: 11, body: words }]);
    expect(r.lines[0].detail).toContain('D11 stays open');
  });

  it('replies to the mention on SHIP-742', () => {
    const words = "I'll install the app on Wednesday.";
    const mention = item('SHIP-742').asks[0];
    const r = done(
      run(
        `Reply to the mention on SHIP-742: ${words}`,
        [['reply', { key: 'SHIP-742', note: 'x' }]],
        words,
      ),
    );
    expect(r.actions).toEqual([
      {
        ...base('SHIP-742'),
        kind: 'mentionReply',
        mentionId: mention.mention!.mentionId,
        label: '@1',
        body: words,
      },
    ]);
    expect(r.lines[0].text).toBe('Reply to Priya Shah on SHIP-742');
  });

  it('marks the SHIP-742 mention seen', () => {
    const mention = item('SHIP-742').asks[0];
    const r = done(run('Mark the SHIP-742 mention seen', [['seen', { key: 'SHIP-742' }]]));
    expect(r.actions).toEqual([
      { ...base('SHIP-742'), kind: 'seen', mentionId: mention.mention!.mentionId, label: '@1' },
    ]);
  });

  it('marks SHIP-743 still true with its revision', () => {
    const r = done(run('Still true on SHIP-743', [['stillTrue', { key: 'SHIP-743' }]]));
    expect(r.actions).toEqual([
      { ...base('SHIP-743'), kind: 'stillTrue', revision: item('SHIP-743').revision },
    ]);
  });

  it('sets a new status on SHIP-743 from the words said', () => {
    const words = 'Unblocked, the host now has pg_dump 18.';
    const r = done(
      run(`New status on SHIP-743: ${words}`, [['status', { key: 'SHIP-743', note: 'x' }]], words),
    );
    expect(r.actions).toEqual([
      {
        ...base('SHIP-743'),
        kind: 'newStatus',
        revision: item('SHIP-743').revision,
        text: words,
        from: 'Blocked: the host has no pg_dump 18 and no Docker.',
      },
    ]);
  });

  it('turns "Approve D1 to D8 on SHIP-907, and tell Priya Shah: …" into two actions in order', () => {
    const words = '50 MB, and trim anything longer than two minutes';
    const r = done(
      run(
        `Approve D1 to D8 on SHIP-907, and tell Priya Shah: ${words}`,
        [
          ['approve', { key: 'SHIP-907', decisions: [1, 2, 3, 4, 5, 6, 7, 8] }],
          ['answer', { person: 'Priya Shah', note: 'x' }],
        ],
        words,
      ),
    );
    expect(r.actions.map((a) => [a.kind, a.key])).toEqual([
      ['approve', 'SHIP-907'],
      ['answer', 'SHIP-892'],
    ]);
    expect(r.lines).toHaveLength(2);
    expect(new Set(r.lines.map((l) => l.id)).size).toBe(2);
  });

  it('fails with ambiguous for a label two items share', () => {
    const f = failed(
      run('Pick personal team for Q1', [
        ['chooseDecision', { label: 'Q1', option: 'Personal team' }],
      ]),
    );
    expect(f.kind).toBe('ambiguous');
    if (f.kind === 'ambiguous')
      expect(f.detail.candidates.map((c) => c.key).sort()).toEqual(['SHIP-906', 'SHIP-924']);
  });

  it('fails with noValidAction for a decision that is not open', () => {
    expect(
      failed(run('Approve D12 on SHIP-907', [['approve', { key: 'SHIP-907', decisions: [12] }]]))
        .kind,
    ).toBe('noValidAction');
    expect(failed(run('Still true on SHIP-906', [['stillTrue', { key: 'SHIP-906' }]])).kind).toBe(
      'noValidAction',
    );
  });

  it('fails as unclear for an answer without a note span', () => {
    // resolveCommand gives it before the spec sees it (amendment 26.5); the spec itself says the same.
    const text = 'Answer Priya Shah';
    expect(failed(run(text, [['answer', { person: 'Priya Shah', note: 'x' }]])).kind).toBe(
      'unclear',
    );
    const direct = spec.toActions(
      [{ action: 'answer', args: { person: 'Priya Shah', note: 'x' } }],
      aTranscript({ text }),
    );
    expect(failed(direct).kind).toBe('unclear');
  });

  it('answers whatNeedsMe with the items in Inbox order and their counts', () => {
    const r = done(run('What needs me?', [['whatNeedsMe', {}]]));
    expect(r.actions).toEqual([]);
    expect(r.lines).toEqual([]);
    expect(r.view).toEqual({
      kind: 'answer',
      content: {
        lead: '6 items wait on you',
        rows: [
          { key: 'SHIP-743', title: 'Nightly backups', counts: 'status to check' },
          { key: 'SHIP-892', title: 'Demo artifacts', counts: '1 question' },
          { key: 'SHIP-907', title: 'Mobile app foundations', counts: '8 decisions' },
          {
            key: 'SHIP-924',
            title: 'Development build on both test phones',
            counts: '1 question',
          },
          { key: 'SHIP-742', title: 'GitHub issues sync', counts: '1 mention' },
          { key: 'SHIP-906', title: 'Voice commands', counts: '2 decisions · 1 question' },
        ],
      },
    });
  });

  it('lists every key, label, name and option as hotwords', () => {
    const words = spec.hotwords();
    for (const w of [
      'SHIP-743',
      'SHIP-906',
      'D1',
      'D11',
      'Q7',
      '@1',
      'Status',
      'Priya Shah',
      'Sam Lee',
      'Joel Dickson',
      '20 MB',
      'Personal team',
      'Wait for org',
      'Any connection',
    ])
      expect(words).toContain(w);
    expect(new Set(words).size).toBe(words.length);
  });

  it('keeps what the correction sheets read in its context, and each open ask', () => {
    const ctx = spec.context() as {
      rows: { key: string; title: string }[];
      states: string[];
      key: string | null;
      items: { key: string; asks: { label: string }[] }[];
    };
    expect(ctx.rows.map((r) => r.key)).toEqual(inbox.items.map((i) => i.key));
    expect(ctx.states).toEqual([]);
    expect(ctx.key).toBeNull();
    expect(ctx.items.find((i) => i.key === 'SHIP-906')!.asks.map((a) => a.label)).toEqual([
      'Q1',
      'D10',
      'D11',
    ]);
  });

  describe('test set', () => {
    const rows = readFileSync(new URL('../voice/testset/inbox.jsonl', import.meta.url), 'utf8')
      .split('\n')
      .filter((l) => l !== '')
      .map((l) => JSON.parse(l) as TestSetRow);
    const FAILS: Record<string, string> = {
      'Pick personal team for Q1': 'ambiguous',
      'Answer question 1: ship it as it is': 'ambiguous',
      'Answer: yes, 50 MB is fine': 'ambiguous',
      'Choose any connection for Q1': 'ambiguous',
      'Approve D12 on SHIP-907': 'noValidAction',
      'Still true on SHIP-906': 'noValidAction',
      'Discuss D2 on SHIP-906: the wrong number': 'noValidAction',
      'Approve D1 on SHIP-743': 'noValidAction',
    };

    it('has a row in inbox.jsonl for every action', () => {
      for (const a of INBOX_ACTIONS)
        expect(
          rows.filter((r) => r.calls.some((c) => c.action === a.name)).length,
          a.name,
        ).toBeGreaterThanOrEqual(3);
      expect(rows.filter((r) => r.calls.length > 1).length).toBeGreaterThanOrEqual(3);
      expect(rows.filter((r) => r.calls.length === 0)).toHaveLength(4);
      expect(rows.every((r) => r.screen === 'inbox' && r.context === undefined)).toBe(true);
    });

    it('replays every row through toActions to its expected calls and note text', () => {
      for (const row of rows) {
        const result = run(
          row.utterance,
          row.calls.map((c) => [c.action, c.args]),
          row.note ?? undefined,
        );
        if (row.calls.length === 0) {
          expect(failed(result).kind, row.utterance).toBe('unclear');
          continue;
        }
        if (FAILS[row.utterance] !== undefined) {
          expect(failed(result).kind, row.utterance).toBe(FAILS[row.utterance]);
          continue;
        }
        const ok = done(result);
        const acting = row.calls.filter((c) => c.action !== 'whatNeedsMe');
        expect(ok.actions, row.utterance).toHaveLength(acting.length);
        expect(ok.rejected, row.utterance).toEqual([]);
        if (row.note !== null) {
          const last = ok.actions[ok.actions.length - 1];
          const body = 'body' in last ? last.body : 'text' in last ? last.text : null;
          expect(body, row.utterance).toBe(row.note);
        }
      }
    });
  });
});
