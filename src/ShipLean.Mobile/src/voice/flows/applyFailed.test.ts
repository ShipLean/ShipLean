import { describe, expect, it } from 'vitest';
import { aPending } from '../../test-support/actions';
import { aTranscript } from '../../test-support/voice';
import { flowHarness } from '../../test-support/voiceFlows';
import { applyGroup, discardApply, onApplyFailed, queueApply, retryApply } from './applyFailed';

const HEARD = 'set it to active, approve D10 and note it runs warm';

function failedBatch() {
  const note = { start: HEARD.indexOf('it runs warm'), end: HEARD.length };
  const h = flowHarness(
    { key: 'SHIP-906', state: 'New', revision: 4, decisions: ['D10'] },
    (text) =>
      text === HEARD
        ? [
            { action: 'set_state', args: { state: 'Active' } },
            { action: 'approve', args: { number: 'D10' } },
            { action: 'add_note', args: {}, noteSpan: note },
          ]
        : [],
  );
  return h;
}

describe('applyFailed', () => {
  it('groups each part: undone is reversed, sent is still applied, anything else is not sent', () => {
    expect(applyGroup(aPending({ state: 'sent', undoOutcome: 'undone' }))).toBe('reversed');
    expect(applyGroup(aPending({ state: 'sent', undoOutcome: 'irreversible' }))).toBe(
      'stillApplied',
    );
    expect(applyGroup(aPending({ state: 'sent', undoOutcome: 'failed' }))).toBe('stillApplied');
    expect(applyGroup(aPending({ state: 'failed' }))).toBe('notSent');
    expect(applyGroup(aPending({ state: 'uncertain' }))).toBe('notSent');
  });

  it('shows the batch with the preview wording, the refused part reason and whether it can be retried', async () => {
    const { v, runner } = failedBatch();
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    const batchId = v.dispatch();
    const [setState, approve, note] = runner.dispatched[0].batch;
    const parts = [
      aPending({
        id: 'p1',
        batchId,
        action: setState,
        origin: 'voice',
        state: 'sent',
        undoOutcome: 'undone',
      }),
      aPending({
        id: 'p2',
        batchId,
        action: approve,
        origin: 'voice',
        state: 'failed',
        failure: 'server',
        error: 'Didn’t send (500).',
        retryable: true,
      }),
      aPending({ id: 'p3', batchId, action: note, origin: 'voice', state: 'failed' }),
    ];

    onApplyFailed(v, batchId, parts);

    expect(v.state()).toEqual({
      kind: 'applyFailed',
      batchId,
      heard: HEARD,
      source: 'voice',
      lines: [
        { id: 'p1', icon: 'flag', text: 'State: New → Active', removable: true },
        { id: 'p2', icon: 'check', text: 'Approve D10', removable: true },
        {
          id: 'p3',
          icon: 'note',
          text: 'Note on SHIP-906',
          detail: 'it runs warm',
          removable: true,
        },
      ],
      reason: 'Didn’t send (500).',
      retryable: true,
    });
  });

  it('Try again with nothing left to send closes the sheet and sends nothing', async () => {
    const { v, runner } = failedBatch();
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    const batchId = v.dispatch(['approve-D10']);
    const parts = [
      aPending({
        batchId,
        action: runner.dispatched[0].batch[0],
        origin: 'voice',
        state: 'sent',
        undoOutcome: 'irreversible',
      }),
    ];
    runner.setPending(parts);
    onApplyFailed(v, batchId, parts);

    retryApply(v);

    expect(runner.dispatched).toHaveLength(1);
    await expect.poll(() => v.state().kind).toBe('idle');
  });

  it('a batch refused with a conflict is left to onConflict', async () => {
    const { v, runner } = failedBatch();
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    const batchId = v.dispatch(['state', 'approve-D10']);
    const [setState, approve] = runner.dispatched[0].batch;
    const parts = [
      aPending({
        id: 'p1',
        batchId,
        action: setState,
        origin: 'voice',
        state: 'failed',
        failure: 'conflict',
        error: 'Someone else changed this.',
      }),
      aPending({ id: 'p2', batchId, action: approve, origin: 'voice', state: 'failed' }),
    ];
    runner.setPending(parts);
    const preview = v.state();

    onApplyFailed(v, batchId, parts);

    expect(v.state()).toBe(preview);
    expect(v.state().kind).toBe('preview');
  });

  it('Try again asks the runner about a part that got no answer and sends nothing again', async () => {
    const { v, runner } = failedBatch();
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    const batchId = v.dispatch(['state', 'approve-D10']);
    const [setState, approve] = runner.dispatched[0].batch;
    const parts = [
      aPending({
        id: 'p-uncertain',
        batchId,
        action: setState,
        origin: 'voice',
        state: 'uncertain',
        error: 'No answer from ShipLean.',
        retryable: true,
      }),
      aPending({ id: 'p-queued', batchId, action: approve, origin: 'voice', state: 'queued' }),
    ];
    runner.setPending(parts);
    onApplyFailed(v, batchId, parts);

    retryApply(v);

    expect(runner.retried).toEqual(['p-uncertain']);
    expect(runner.dispatched).toHaveLength(1);
    await expect.poll(() => v.state().kind).toBe('idle');
  });

  it('Send when online leaves a part that got no answer, and the parts behind it, to the runner', async () => {
    const { v, runner } = failedBatch();
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    const batchId = v.dispatch(['state', 'approve-D10']);
    const [setState, approve] = runner.dispatched[0].batch;
    const parts = [
      aPending({
        id: 'p-uncertain',
        batchId,
        action: setState,
        origin: 'voice',
        state: 'uncertain',
        retryable: true,
      }),
      aPending({ id: 'p-queued', batchId, action: approve, origin: 'voice', state: 'queued' }),
    ];
    runner.setPending(parts);
    onApplyFailed(v, batchId, parts);

    queueApply(v);

    expect(runner.dispatched).toHaveLength(1);
    expect(runner.retried).toEqual([]);
    await expect.poll(() => v.state().kind).toBe('idle');
  });

  it('Try again on a typed batch asks the runner to send the same refused parts again, with no new batch', async () => {
    const { v, runner } = failedBatch();
    await v.resolve(aTranscript({ text: HEARD }), { source: 'typed' });
    const batchId = v.dispatch(['state', 'approve-D10']);
    const [setState, approve] = runner.dispatched[0].batch;
    const parts = [
      aPending({
        id: 'p1',
        batchId,
        action: setState,
        origin: 'typed',
        state: 'failed',
        failure: 'server',
        error: 'Didn’t send (500).',
        retryable: true,
      }),
      aPending({ id: 'p2', batchId, action: approve, origin: 'typed', state: 'failed' }),
    ];
    runner.setPending(parts);
    onApplyFailed(v, batchId, parts);

    retryApply(v);

    expect(runner.retried).toEqual(['p1', 'p2']);
    expect(runner.dispatched).toHaveLength(1);
    expect(v.state().kind).not.toBe('applying');
    await expect.poll(() => v.state().kind).toBe('idle');
  });

  it('Send when online on a batch the runner reversed nothing of retries its failed parts and closes', async () => {
    const { v, runner } = failedBatch();
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    const batchId = v.dispatch(['state', 'approve-D10']);
    const [setState, approve] = runner.dispatched[0].batch;
    // Dispatched offline, so not reversed: the state change stayed applied.
    const parts = [
      aPending({ id: 'p1', batchId, action: setState, origin: 'voice', state: 'sent' }),
      aPending({
        id: 'p2',
        batchId,
        action: approve,
        origin: 'voice',
        state: 'failed',
        failure: 'server',
        retryable: true,
      }),
    ];
    runner.setPending(parts);
    onApplyFailed(v, batchId, parts);

    queueApply(v);

    expect(runner.retried).toEqual(['p2']);
    expect(runner.dispatched).toHaveLength(1);
    await expect.poll(() => v.state().kind).toBe('idle');
  });

  it('Try again and Send when online retire the old refused parts once the new batch of a reversed batch is out', async () => {
    for (const choose of [retryApply, queueApply]) {
      const { v, runner } = failedBatch();
      await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
      const batchId = v.dispatch(['state', 'approve-D10', 'note']);
      const [setState, approve, note] = runner.dispatched[0].batch;
      const parts = [
        aPending({
          id: 'p1',
          batchId,
          action: setState,
          origin: 'voice',
          state: 'sent',
          undoOutcome: 'undone',
        }),
        aPending({
          id: 'p2',
          batchId,
          action: approve,
          origin: 'voice',
          state: 'failed',
          failure: 'server',
          retryable: true,
        }),
        aPending({ id: 'p3', batchId, action: note, origin: 'voice', state: 'failed' }),
      ];
      runner.setPending(parts);
      onApplyFailed(v, batchId, parts);

      choose(v);

      expect(runner.dispatched).toHaveLength(2);
      expect(runner.retired).toEqual([['p2', 'p3']]);
      expect(runner.pending().map((p) => p.id)).toEqual(['p1']);
    }
  });

  it('gives each line its part id, so a part that left pending() moves no other line', async () => {
    const { v, runner } = failedBatch();
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    const batchId = v.dispatch(['state', 'approve-D10']);
    const [setState, approve] = runner.dispatched[0].batch;
    const parts = [
      aPending({ id: 'p1', batchId, action: setState, origin: 'voice', state: 'failed' }),
      aPending({ id: 'p2', batchId, action: approve, origin: 'voice', state: 'failed' }),
    ];

    onApplyFailed(v, batchId, parts);

    const s = v.state();
    expect(s.kind === 'applyFailed' && s.lines.map((l) => [l.id, l.text])).toEqual([
      ['p1', 'State: New → Active'],
      ['p2', 'Approve D10'],
    ]);
  });

  it('Discard sends nothing and closes the sheet', async () => {
    const { v, runner } = failedBatch();
    await v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
    const batchId = v.dispatch();
    const parts = runner.dispatched[0].batch.map((action, i) =>
      aPending({ id: `p${i}`, batchId, action, origin: 'voice', state: 'failed' }),
    );
    runner.setPending(parts);
    onApplyFailed(v, batchId, parts);

    discardApply(v);

    await expect.poll(() => v.state().kind).toBe('idle');
    expect(runner.dispatched).toHaveLength(1);
  });
});
