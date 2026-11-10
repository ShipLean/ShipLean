import { describe, expect, it } from 'vitest';
import { aPending } from '../../test-support/actions';
import { aTranscript } from '../../test-support/voice';
import { flowHarness } from '../../test-support/voiceFlows';
import { applyGroup, discardApply, onApplyFailed, retryApply } from './applyFailed';

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
        { id: 'state', icon: 'flag', text: 'State: New → Active', removable: true },
        { id: 'approve-D10', icon: 'check', text: 'Approve D10', removable: true },
        {
          id: 'note',
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
