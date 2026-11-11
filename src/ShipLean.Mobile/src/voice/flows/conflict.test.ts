import { afterEach, describe, expect, it, vi } from 'vitest';
import { aPending } from '../../test-support/actions';
import { aTranscript } from '../../test-support/voice';
import { flowHarness, type FlowContext } from '../../test-support/voiceFlows';
import type { ParsedCall } from '../contracts';
import { CHANGED_HERE, REFRESH_WAIT_MS, onConflict } from './conflict';

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

// Sends both lines and has the server refuse the state change as stale; the approval behind it never went.
async function refusedAsStale() {
  const h = sentBatch();
  await h.v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
  const batchId = h.v.dispatch(['state', 'approve-D10']);
  const [setState, approve] = h.runner.dispatched[0].batch;
  h.runner.setPending([
    aPending({
      id: 'p1',
      batchId,
      action: setState,
      origin: 'voice',
      state: 'failed',
      failure: 'conflict',
      error: 'Changed on the server. Open SHIP-906 to check.',
    }),
    aPending({ id: 'p2', batchId, action: approve, origin: 'voice', state: 'failed' }),
  ]);
  return { ...h, batchId };
}

afterEach(() => {
  vi.useRealTimers();
});

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

  it('waits for the screen to read the item again before rebuilding, so the confirm sends the server revision', async () => {
    const { v, runner, context, batchId, rendered } = await refusedAsStale();

    // The 409 comes before the screen has read SHIP-906 again: its spec still builds revision 4.
    onConflict(v, batchId, []);
    expect(v.state()).toEqual({ kind: 'applying', batchId });
    rendered();
    expect(v.state()).toEqual({ kind: 'applying', batchId });

    // The refetch lands: Priya resolved it meanwhile.
    context.state = 'Resolved';
    context.revision = 5;
    rendered();

    const s = v.state();
    if (s.kind !== 'preview') throw new Error(`expected a preview, got ${s.kind}`);
    expect(s.lines.map((l) => l.text)).toEqual(['State: Resolved → Active', 'Approve D10']);
    expect(s.marks).toEqual([{ lineId: 'state', note: CHANGED_HERE }]);
    expect(runner.dispatched).toHaveLength(1);
    v.dispatch();
    expect(runner.dispatched[1].batch[0]).toMatchObject({ kind: 'setState', revision: 5 });
  });

  it('says the change did not send when the screen never reads the item again', async () => {
    vi.useFakeTimers();
    const { v, runner, batchId, rendered } = await refusedAsStale();

    onConflict(v, batchId, []);
    vi.advanceTimersByTime(REFRESH_WAIT_MS);
    rendered();

    expect(v.state()).toMatchObject({
      kind: 'applyFailed',
      batchId,
      reason: 'Changed on the server. Open SHIP-906 to check.',
      retryable: false,
    });
    expect(runner.dispatched).toHaveLength(1);
  });
});
