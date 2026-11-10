import { describe, expect, it } from 'vitest';
import { aPending } from '../../test-support/actions';
import { aTranscript } from '../../test-support/voice';
import { flowHarness, type FlowContext } from '../../test-support/voiceFlows';
import type { ParsedCall } from '../contracts';
import { CHANGED_HERE, onConflict } from './conflict';

const HEARD = 'set it to active and approve D10';
const CALLS: ParsedCall[] = [
  { action: 'set_state', args: { state: 'Active' } },
  { action: 'approve', args: { number: 'D10' } },
];

function sentBatch() {
  const context: FlowContext = { key: 'SHIP-906', state: 'New', revision: 4, decisions: ['D10'] };
  const h = flowHarness(context, (text) => (text === HEARD ? CALLS : []));
  return { ...h, context };
}

describe('conflict', () => {
  it('a stale-revision failure brings the preview back with the changed row marked, and nothing is dispatched until confirmed', async () => {
    const { v, runner, context } = sentBatch();
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    const batchId = v.dispatch(['state', 'approve-D10']);
    const [setState, approve] = runner.dispatched[0].batch;
    runner.setPending([
      aPending({
        id: 'p1',
        batchId,
        action: setState,
        origin: 'voice',
        state: 'failed',
        failure: 'conflict',
      }),
      aPending({ id: 'p2', batchId, action: approve, origin: 'voice', state: 'failed' }),
    ]);
    // Priya resolved it meanwhile.
    context.state = 'Resolved';
    context.revision = 5;

    onConflict(v, batchId, []);

    const s = v.state();
    if (s.kind !== 'preview') throw new Error(`expected a preview, got ${s.kind}`);
    expect(s.lines.map((l) => [l.id, l.text])).toEqual([
      ['state', 'State: Resolved → Active'],
      ['approve-D10', 'Approve D10'],
    ]);
    expect(s.marks).toEqual([{ lineId: 'state', note: CHANGED_HERE }]);
    expect(s.removed).toEqual([]);
    expect(runner.dispatched).toHaveLength(1);

    // Confirmed: the rebuilt actions go, at the current revision.
    v.dispatch();
    expect(runner.dispatched).toHaveLength(2);
    expect(runner.dispatched[1].batch[0]).toMatchObject({
      kind: 'setState',
      from: 'Resolved',
      revision: 5,
    });
  });

  it('keeps the lines the person removed before Apply removed', async () => {
    const { v, runner } = sentBatch();
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    const batchId = v.dispatch(['approve-D10']);
    runner.setPending([
      aPending({
        batchId,
        action: runner.dispatched[0].batch[0],
        origin: 'voice',
        state: 'failed',
        failure: 'conflict',
      }),
    ]);

    onConflict(v, batchId, []);

    expect(v.state()).toMatchObject({ kind: 'preview', removed: ['state'] });
  });

  it('leaves a batch that came from a tap alone', async () => {
    const { v, runner } = sentBatch();
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    const before = v.state();
    runner.setPending([
      aPending({ batchId: 'tap-batch', origin: 'tap', state: 'failed', failure: 'conflict' }),
    ]);

    onConflict(v, 'tap-batch', []);

    expect(v.state()).toBe(before);
  });
});
