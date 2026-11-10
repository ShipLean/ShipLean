import { fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import type { Pending } from '../../actions/types';
import { aPending } from '../../test-support/actions';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import { aTranscript } from '../../test-support/voice';
import { flowHarness } from '../../test-support/voiceFlows';
import { discardApply, onApplyFailed, queueApply, retryApply } from '../flows/applyFailed';
import type { VoiceUiStateOf } from '../internals';
import { ApplyFailedSheet } from './ApplyFailedSheet';
import {
  applyFailedDiscardId,
  applyFailedGroupId,
  applyFailedHeadingId,
  applyFailedLineId,
  applyFailedMayHaveLandedId,
  applyFailedQueueId,
  applyFailedReasonId,
  applyFailedRetryId,
} from './ApplyFailedSheet.testIds';

const HEARD = 'set it to active, approve D10 and note it runs warm';
const NOTE = { start: HEARD.indexOf('it runs warm'), end: HEARD.length };

// A voice command applied as one batch: state, approve and note, in that order.
async function sentBatch() {
  const h = flowHarness(
    { key: 'SHIP-906', state: 'New', revision: 4, decisions: ['D10'] },
    (text) =>
      text === HEARD
        ? [
            { action: 'set_state', args: { state: 'Active' } },
            { action: 'approve', args: { number: 'D10' } },
            { action: 'add_note', args: {}, noteSpan: NOTE },
          ]
        : [],
  );
  await h.v.resolve(aTranscript({ text: HEARD }), { source: 'voice' });
  const batchId = h.v.dispatch();
  const [setState, approve, note] = h.runner.dispatched[0].batch;
  return { ...h, batchId, setState, approve, note };
}

// The runner reported the batch failed with these parts.
async function failed(h: Awaited<ReturnType<typeof sentBatch>>, parts: Pending[]) {
  h.runner.setPending(parts);
  onApplyFailed(h.v, h.batchId, parts);
  const state = h.v.state() as VoiceUiStateOf<'applyFailed'>;
  await renderWithRunner(
    <ApplyFailedSheet
      state={state}
      onRetry={() => retryApply(h.v)}
      onQueue={() => queueApply(h.v)}
      onDiscard={() => discardApply(h.v)}
    />,
    { runner: h.runner },
  );
  return state;
}

// An approve that stayed (approve can't be undone), a state change the runner reversed, and a note refused by the
// server.
function partlyApplied(h: Awaited<ReturnType<typeof sentBatch>>): Pending[] {
  return [
    aPending({
      id: 'p1',
      batchId: h.batchId,
      action: h.setState,
      origin: 'voice',
      state: 'sent',
      undoOutcome: 'undone',
    }),
    aPending({
      id: 'p2',
      batchId: h.batchId,
      action: h.approve,
      origin: 'voice',
      state: 'sent',
      undoOutcome: 'irreversible',
    }),
    aPending({
      id: 'p3',
      batchId: h.batchId,
      action: h.note,
      origin: 'voice',
      state: 'failed',
      failure: 'server',
      error: 'Didn’t send (500).',
      retryable: true,
    }),
  ];
}

describe('applyFailed', () => {
  it('a failed voice batch with nothing applied shows Nothing was changed with the reason', async () => {
    const h = await sentBatch();
    await failed(h, [
      aPending({
        id: 'p1',
        batchId: h.batchId,
        action: h.setState,
        origin: 'voice',
        state: 'uncertain',
        error: 'No answer from ShipLean.',
        retryable: true,
      }),
      aPending({
        id: 'p2',
        batchId: h.batchId,
        action: h.approve,
        origin: 'voice',
        state: 'failed',
      }),
      aPending({ id: 'p3', batchId: h.batchId, action: h.note, origin: 'voice', state: 'failed' }),
    ]);

    expect(screen.getByTestId(applyFailedHeadingId)).toHaveTextContent('Nothing was changed');
    expect(screen.getByTestId(applyFailedReasonId)).toHaveTextContent('No answer from ShipLean.');
    const notSent = within(screen.getByTestId(applyFailedGroupId('notSent')));
    expect(notSent.getByTestId(applyFailedLineId('state'))).toHaveTextContent(
      /^State: New → Active/,
    );
    expect(notSent.getByTestId(applyFailedLineId('approve-D10'))).toBeOnTheScreen();
    expect(notSent.getByTestId(applyFailedLineId('note'))).toBeOnTheScreen();
    expect(screen.getByTestId(applyFailedMayHaveLandedId('state'))).toHaveTextContent(
      'No answer: it may have landed',
    );
    expect(screen.queryByTestId(applyFailedMayHaveLandedId('note'))).toBeNull();
    expect(screen.queryByTestId(applyFailedGroupId('stillApplied'))).toBeNull();
    expect(screen.queryByTestId(applyFailedGroupId('reversed'))).toBeNull();
  });
});

describe('ApplyFailedSheet', () => {
  it('an approve that stayed is listed as still applied, a reversed status as reversed and the rest as not sent, without Nothing was changed', async () => {
    const h = await sentBatch();
    await failed(h, partlyApplied(h));

    const group = (g: string) => within(screen.getByTestId(applyFailedGroupId(g)));
    expect(group('stillApplied').getByTestId(applyFailedLineId('approve-D10'))).toHaveTextContent(
      'Approve D10',
    );
    expect(group('stillApplied').queryByTestId(applyFailedLineId('state'))).toBeNull();
    expect(group('reversed').getByTestId(applyFailedLineId('state'))).toHaveTextContent(
      'State: New → Active',
    );
    expect(group('notSent').getByTestId(applyFailedLineId('note'))).toHaveTextContent(
      'Note on SHIP-906it runs warm',
    );
    expect(screen.getByTestId(applyFailedHeadingId)).not.toHaveTextContent('Nothing was changed');
    expect(screen.getByTestId(applyFailedReasonId)).toHaveTextContent('Didn’t send (500).');
  });

  it('Try again dispatches the reversed and unsent actions, Send when online queues them, Discard sends nothing', async () => {
    const h = await sentBatch();
    const state = await failed(h, partlyApplied(h));

    await fireEvent.press(screen.getByTestId(applyFailedRetryId));
    expect(h.runner.dispatched).toHaveLength(2);
    expect(h.runner.dispatched[1].batch).toEqual([h.setState, h.note]);
    expect(h.runner.dispatched[1].opts).toMatchObject({ origin: 'voice', allOrNothing: true });
    expect(h.v.state()).toEqual({ kind: 'applying', batchId: h.runner.dispatched[1].batchId });

    h.v.set(state);
    await fireEvent.press(screen.getByTestId(applyFailedQueueId));
    expect(h.runner.dispatched).toHaveLength(3);
    expect(h.runner.dispatched[2].batch).toEqual([h.setState, h.note]);
    await waitFor(() => expect(h.v.state().kind).toBe('idle'));

    h.v.set(state);
    await fireEvent.press(screen.getByTestId(applyFailedDiscardId));
    await waitFor(() => expect(h.v.state().kind).toBe('idle'));
    expect(h.runner.dispatched).toHaveLength(3);
  });

  it('offers only Discard when sending again cannot help', async () => {
    const h = await sentBatch();
    await failed(h, [
      aPending({
        id: 'p1',
        batchId: h.batchId,
        action: h.setState,
        origin: 'voice',
        state: 'failed',
        failure: 'validation',
        error: 'The state is not allowed.',
        retryable: false,
      }),
    ]);

    expect(screen.queryByTestId(applyFailedRetryId)).toBeNull();
    expect(screen.queryByTestId(applyFailedQueueId)).toBeNull();
    expect(screen.getByTestId(applyFailedDiscardId)).toBeOnTheScreen();
  });
});
