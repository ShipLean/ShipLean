import { beforeEach, describe, expect, it, vi } from 'vitest';
import { aTranscript } from '../../test-support/voice';
import { flowHarness, type FlowContext } from '../../test-support/voiceFlows';
import type { ParsedCall } from '../contracts';
import { requestComposer } from '../typed/composerRequest';
import { fixHeard, startFix } from './fix';

vi.mock('../typed/composerRequest', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../typed/composerRequest')>();
  return { ...actual, requestComposer: vi.fn() };
});

const HEARD = 'Set it to active, approve D10 and note the S22 numbers are fine for v1';
const NOTE = 'the S22 numbers are fine for v1';
const noteSpan = { start: HEARD.indexOf(NOTE), end: HEARD.indexOf(NOTE) + NOTE.length };

const context = (): FlowContext => ({
  key: 'SHIP-906',
  state: 'New',
  revision: 4,
  decisions: ['D10', 'D11'],
});

// The model: the first command, the first command with its correction, or a fresh command after "start over".
function model(text: string): ParsedCall[] {
  const command = (n: string): ParsedCall[] => [
    { action: 'set_state', args: { state: 'Active' } },
    { action: 'approve', args: { number: n } },
    { action: 'add_note', args: {}, noteSpan },
  ];
  if (text === HEARD) return command('D10');
  if (text === `${HEARD} No, D11, not D10`) return command('D11');
  if (text === 'move it to resolved') return [{ action: 'set_state', args: { state: 'Resolved' } }];
  return [];
}

beforeEach(() => {
  vi.mocked(requestComposer).mockClear();
});

describe('fix', () => {
  it('Fix with "No, D11, not D10" changes only that row and marks it Fixed · was D10', async () => {
    const { v, parsed } = flowHarness(context(), model);
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    startFix(v);
    expect(v.state()).toMatchObject({ kind: 'preview', fixing: true });

    await fixHeard(v, aTranscript({ text: 'No, D11, not D10' }));

    expect(parsed).toEqual([HEARD, `${HEARD} No, D11, not D10`]);
    const s = v.state();
    if (s.kind !== 'preview') throw new Error(`expected a preview, got ${s.kind}`);
    expect(s.heard).toBe(`${HEARD} No, D11, not D10`);
    expect(s.lines.map((l) => [l.id, l.text, l.detail])).toEqual([
      ['state', 'State: New → Active', undefined],
      ['approve-D11', 'Approve D11', undefined],
      ['note', 'Note on SHIP-906', NOTE],
    ]);
    expect(s.marks).toEqual([{ lineId: 'approve-D11', note: 'Fixed · was D10' }]);
    expect(s.fixing).toBe(false);
  });

  it('Fix with "start over" replaces the preview', async () => {
    const { v, parsed } = flowHarness(context(), model);
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    startFix(v);

    await fixHeard(v, aTranscript({ text: 'Start over, move it to resolved' }));

    expect(parsed).toEqual([HEARD, 'move it to resolved']);
    const s = v.state();
    if (s.kind !== 'preview') throw new Error(`expected a preview, got ${s.kind}`);
    expect(s.heard).toBe('move it to resolved');
    expect(s.lines.map((l) => l.text)).toEqual(['State: New → Resolved']);
    expect(s.marks).toEqual([]);
  });

  it('Fix with only "start over" closes the preview without parsing', async () => {
    const { v, parsed } = flowHarness(context(), model);
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    startFix(v);

    await fixHeard(v, aTranscript({ text: 'start over' }));

    expect(parsed).toEqual([HEARD]);
    expect(v.state()).toEqual({ kind: 'idle', permission: 'granted' });
  });

  it('Fix on a typed preview opens the composer with the text', async () => {
    const { v } = flowHarness(context(), model);
    await v.resolve({ text: HEARD, words: [], durationMs: 0 }, { source: 'typed' });

    startFix(v);

    expect(requestComposer).toHaveBeenCalledWith({
      mode: 'command',
      text: HEARD,
      reason: 'edit',
      questionNumber: null,
    });
    expect(v.sets.some((s) => s.kind === 'preview' && s.fixing)).toBe(false);
    await vi.waitFor(() => expect(v.state().kind).toBe('idle'));
  });
});
