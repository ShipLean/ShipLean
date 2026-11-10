import { describe, expect, it } from 'vitest';
import type { AppAction } from '../../actions/types';
import { aTranscript } from '../../test-support/voice';
import { classify, complete, type Outcome } from './classify';

const t = aTranscript({ text: 'set SHIP-906 to active and assign SHIP-4321 to Priya' });
const action = (key: string): AppAction => ({
  area: 'workItem',
  kind: 'setState',
  itemId: null,
  key,
});
const line = (key: string) => ({ id: `line-${key}`, icon: 'state', text: key, removable: true });
const ok = (key: string): Outcome<AppAction> => ({
  ok: true,
  result: { ok: true, actions: [action(key)], lines: [line(key)], rejected: [], view: null },
});
const assign = { action: 'assign', args: { item: 'SHIP-4321', person: 'Priya' } };
const noMatch = {
  kind: 'noMatch' as const,
  detail: { spoken: 'SHIP-4321', role: 'item', span: null },
};

describe('classify', () => {
  it('reports a partial command as ok with the failed call rejected, not as a whole failure', () => {
    const result = classify([ok('SHIP-906'), { ok: false, call: assign, failure: noMatch }], t);
    expect(result).toEqual({
      ok: true,
      actions: [action('SHIP-906')],
      lines: [line('SHIP-906')],
      rejected: [
        {
          call: assign,
          failure: {
            kind: 'noMatch',
            detail: { spoken: 'SHIP-4321', role: 'item', span: { start: 34, end: 43 } },
          },
        },
      ],
      view: null,
    });
  });
  it('keeps lines[i] with actions[i] across calls, in the order said', () => {
    const result = classify([ok('SHIP-1'), ok('SHIP-2')], t);
    if (!result.ok) throw new Error('expected ok');
    expect(result.actions.map((a) => a.key)).toEqual(['SHIP-1', 'SHIP-2']);
    expect(result.lines.map((l) => l.id)).toEqual(['line-SHIP-1', 'line-SHIP-2']);
  });
  it('gives two notes on the same item two distinct line ids, each still with its action', () => {
    const note = (text: string): Outcome<AppAction> => ({
      ok: true,
      result: {
        ok: true,
        actions: [action(text)],
        lines: [{ id: 'comment:SHIP-906', icon: 'note', text, removable: true }],
        rejected: [],
        view: null,
      },
    });
    const result = classify([note('a'), note('b'), note('c')], t);
    if (!result.ok) throw new Error('expected ok');
    expect(result.lines.map((l) => l.id)).toEqual([
      'comment:SHIP-906',
      'comment:SHIP-906#2',
      'comment:SHIP-906#3',
    ]);
    expect(result.lines.map((l) => l.text)).toEqual(result.actions.map((a) => a.key));
  });
  it('fails with the first rejection when every call is rejected', () => {
    const webOnly = { kind: 'webOnly' as const, detail: { action: 'split', where: 'the web' } };
    const split = { action: 'split', args: {} };
    expect(
      classify(
        [
          { ok: false, call: split, failure: webOnly },
          { ok: false, call: assign, failure: noMatch },
        ],
        t,
      ),
    ).toEqual({ ok: false, failure: webOnly });
  });
  it('fills a missing span from the transcript and a missing action from the call', () => {
    expect(complete(noMatch, assign, t)).toEqual({
      kind: 'noMatch',
      detail: { spoken: 'SHIP-4321', role: 'item', span: { start: 34, end: 43 } },
    });
    expect(
      complete({ ...noMatch, detail: { ...noMatch.detail, spoken: 'Nobody' } }, assign, t),
    ).toEqual({
      kind: 'noMatch',
      detail: { spoken: 'Nobody', role: 'item', span: null },
    });
    expect(
      complete(
        { kind: 'noValidAction', detail: { action: null, reason: 'noSuchState' } },
        assign,
        t,
      ),
    ).toEqual({ kind: 'noValidAction', detail: { action: 'assign', reason: 'noSuchState' } });
  });
});
