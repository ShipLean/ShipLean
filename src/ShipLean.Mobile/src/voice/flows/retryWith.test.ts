import { describe, expect, it } from 'vitest';
import { aTranscript } from '../../test-support/voice';
import { flowHarness, type FlowContext } from '../../test-support/voiceFlows';
import type { ParsedCall } from '../contracts';
import { retryWith } from './retryWith';

const context = (): FlowContext => ({
  key: 'SHIP-906',
  state: 'New',
  revision: 4,
  decisions: ['D10'],
});

describe('retryWith', () => {
  it('replaces one span and previews again', async () => {
    const heard = 'approve D1O and set it to active';
    const calls: ParsedCall[] = [
      { action: 'approve', args: { number: 'D1O' } },
      { action: 'set_state', args: { state: 'Active' } },
    ];
    const { v, parsed } = flowHarness(context(), (text) => (text === heard ? calls : []));
    await v.resolve(aTranscript({ text: heard }), { source: 'voice' });
    // The unknown decision is struck out with the reason; the state change stands.
    expect(v.state()).toMatchObject({ kind: 'preview' });

    const start = heard.indexOf('D1O');
    await retryWith(v, { span: { start, end: start + 3 }, value: 'D10' });

    expect(parsed).toEqual([heard]);
    const s = v.state();
    if (s.kind !== 'preview') throw new Error(`expected a preview, got ${s.kind}`);
    expect(s.heard).toBe(heard);
    expect(s.source).toBe('voice');
    expect(s.lines.map((l) => [l.id, l.text])).toEqual([
      ['approve-D10', 'Approve D10'],
      ['state', 'State: New → Active'],
    ]);
    expect(v.last()?.calls[0]).toEqual({ action: 'approve', args: { number: 'D10' } });
  });

  it('as a note previews the note action', async () => {
    const heard = 'the S22 numbers are fine for v1';
    const { v, parsed } = flowHarness(context(), () => []);
    await v.resolve(aTranscript({ text: heard }), { source: 'voice' });
    expect(v.state()).toMatchObject({ kind: 'failure' });

    await retryWith(v, { asNote: true });

    expect(parsed).toEqual([heard]);
    expect(v.state()).toMatchObject({
      kind: 'preview',
      heard,
      lines: [{ id: 'note', text: 'Note on SHIP-906', detail: heard }],
    });
    expect(v.last()?.calls).toEqual([
      { action: 'add_note', args: {}, noteSpan: { start: 0, end: heard.length } },
    ]);
  });
});
