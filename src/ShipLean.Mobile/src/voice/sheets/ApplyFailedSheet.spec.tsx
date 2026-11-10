import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
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
  applyFailedDiscardNoteId,
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
    expect(notSent.getByTestId(applyFailedLineId('p1'))).toHaveTextContent(/^State: New → Active/);
    expect(notSent.getByTestId(applyFailedLineId('p2'))).toBeOnTheScreen();
    expect(notSent.getByTestId(applyFailedLineId('p3'))).toBeOnTheScreen();
    expect(screen.getByTestId(applyFailedMayHaveLandedId('p1'))).toHaveTextContent(
      'No answer: it may have landed',
    );
    expect(screen.queryByTestId(applyFailedMayHaveLandedId('p3'))).toBeNull();
    expect(screen.queryByTestId(applyFailedGroupId('stillApplied'))).toBeNull();
    expect(screen.queryByTestId(applyFailedGroupId('reversed'))).toBeNull();
  });
});

describe('ApplyFailedSheet', () => {
  it('an approve that stayed is listed as still applied, a reversed status as reversed and the rest as not sent, without Nothing was changed', async () => {
    const h = await sentBatch();
    await failed(h, partlyApplied(h));

    const group = (g: string) => within(screen.getByTestId(applyFailedGroupId(g)));
    expect(group('stillApplied').getByTestId(applyFailedLineId('p2'))).toHaveTextContent(
      'Approve D10',
    );
    expect(group('stillApplied').queryByTestId(applyFailedLineId('p1'))).toBeNull();
    expect(group('reversed').getByTestId(applyFailedLineId('p1'))).toHaveTextContent(
      'State: New → Active',
    );
    expect(group('notSent').getByTestId(applyFailedLineId('p3'))).toHaveTextContent(
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

  it('lists a sent part the runner did not reverse under Still applied, with no group for parts being reversed', async () => {
    const h = await sentBatch();
    // A batch dispatched offline: the runner never reverses it, so the state change keeps undoOutcome null.
    await failed(h, [
      aPending({
        id: 'p1',
        batchId: h.batchId,
        action: h.setState,
        origin: 'voice',
        state: 'sent',
      }),
      aPending({
        id: 'p2',
        batchId: h.batchId,
        action: h.approve,
        origin: 'voice',
        state: 'failed',
        failure: 'server',
        error: 'Didn’t send (500).',
        retryable: true,
      }),
    ]);

    expect(screen.getByTestId(applyFailedHeadingId)).toHaveTextContent(
      'Some changes stayed applied',
    );
    expect(
      within(screen.getByTestId(applyFailedGroupId('stillApplied'))).getByTestId(
        applyFailedLineId('p1'),
      ),
    ).toBeOnTheScreen();
    expect(screen.queryByTestId(applyFailedGroupId('reversing'))).toBeNull();
  });

  it('keeps a line in the group it opened in when its part leaves pending()', async () => {
    const h = await sentBatch();
    const [reversed, stayed, refused] = partlyApplied(h);
    await failed(h, [reversed, stayed, refused]);

    // The sent parts are pruned while the sheet is open: only the refused part is left.
    await act(async () => h.runner.setPending([refused]));

    const group = (g: string) => within(screen.getByTestId(applyFailedGroupId(g)));
    expect(group('reversed').getByTestId(applyFailedLineId('p1'))).toBeOnTheScreen();
    expect(group('stillApplied').getByTestId(applyFailedLineId('p2'))).toBeOnTheScreen();
    expect(group('notSent').queryByTestId(applyFailedLineId('p1'))).toBeNull();
    expect(group('notSent').queryByTestId(applyFailedLineId('p2'))).toBeNull();
    expect(screen.getByTestId(applyFailedHeadingId)).toHaveTextContent(
      'Some changes stayed applied',
    );
  });

  it('keeps each line with its own part when an earlier part leaves pending()', async () => {
    const h = await sentBatch();
    const all = [
      aPending({
        id: 'p1',
        batchId: h.batchId,
        action: h.setState,
        origin: 'voice',
        state: 'failed',
        failure: 'server',
      }),
      aPending({
        id: 'p2',
        batchId: h.batchId,
        action: h.approve,
        origin: 'voice',
        state: 'sent',
        undoOutcome: 'undone',
      }),
      aPending({
        id: 'p3',
        batchId: h.batchId,
        action: h.note,
        origin: 'voice',
        state: 'sent',
        undoOutcome: 'irreversible',
      }),
    ];
    const [, reversed, stayed] = all;
    await failed(h, all);

    // The refused part leaves pending() (its area unregistered): the others stay in their groups.
    await act(async () => h.runner.setPending([reversed, stayed]));

    const group = (g: string) => within(screen.getByTestId(applyFailedGroupId(g)));
    expect(group('reversed').getByTestId(applyFailedLineId('p2'))).toHaveTextContent('Approve D10');
    expect(group('stillApplied').getByTestId(applyFailedLineId('p3'))).toHaveTextContent(
      /^Note on SHIP-906/,
    );
  });

  it('Discard on a batch with no answer drops what waits behind it and says what may have landed', async () => {
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
        state: 'queued',
      }),
    ]);

    expect(screen.getByTestId(applyFailedDiscardNoteId)).toHaveTextContent(
      'Discard: nothing more will be sent. A change with no answer stays if it landed.',
    );
    await fireEvent.press(screen.getByTestId(applyFailedDiscardId));

    expect(h.runner.discarded).toEqual([h.batchId]);
    expect(h.runner.pending().map((p) => p.id)).toEqual(['p1']);
    expect(h.runner.dispatched).toHaveLength(1);
    await waitFor(() => expect(h.v.state().kind).toBe('idle'));
  });
});
