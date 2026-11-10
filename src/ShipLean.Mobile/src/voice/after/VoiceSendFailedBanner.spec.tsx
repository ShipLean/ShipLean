import { fireEvent, screen } from '@testing-library/react-native';
import type { AppAction, Executor } from '../../actions/types';
import { aPending, fakeRunner } from '../../test-support/actions';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import { VoiceSendFailedBanner } from './VoiceSendFailedBanner';
import {
  voiceSendFailedAppliedId,
  voiceSendFailedDismissId,
  voiceSendFailedRefusedId,
} from './VoiceSendFailedBanner.testIds';

const TEXT: Record<string, string> = {
  setState: 'State → Active',
  approve: 'Approve D10',
  addNote: 'Your note',
};

const executor: Executor<AppAction> = {
  area: 'workItem',
  send: async () => undefined,
  describe: (a) => ({ id: a.kind, icon: 'dot', text: TEXT[a.kind] ?? a.kind, removable: true }),
  reversible: () => true,
};

const part = (id: string, kind: string, batchId = 'b1', itemId = 'item-1', key = 'SHIP-906') =>
  aPending({
    id,
    batchId,
    origin: 'voice',
    state: 'sent',
    action: { area: 'workItem', kind, itemId, key },
  });

// SHIP-906's queued batch sent later: the state and the note went through, D10's approval was refused.
function sentLater() {
  const runner = fakeRunner();
  runner.register(executor);
  runner.setPending([
    part('p1', 'setState'),
    {
      ...part('p2', 'approve'),
      state: 'failed',
      failure: 'validation',
      error: 'Claude Code withdrew D10.',
    },
    part('p3', 'addNote'),
    {
      ...part('o1', 'setState', 'b2', 'item-2', 'SHIP-921'),
      state: 'failed',
      failure: 'conflict',
      error: 'Priya changed it first.',
    },
  ]);
  return runner;
}

describe('VoiceSendFailedBanner', () => {
  it('names the refused change and the reason, and the other changes stay shown as applied', async () => {
    const runner = sentLater();
    await renderWithRunner(<VoiceSendFailedBanner itemId="item-1" itemKey="SHIP-906" />, {
      runner,
    });

    expect(screen.getByText('1 of 3 voice changes wasn’t applied · SHIP-906')).toBeOnTheScreen();
    const refused = screen.getByTestId(voiceSendFailedRefusedId('p2'));
    expect(refused).toHaveTextContent('“Approve D10”Claude Code withdrew D10.');
    expect(screen.getByTestId(voiceSendFailedAppliedId('b1'))).toHaveTextContent(
      'State → Active, Your note went through.',
    );
    for (const id of ['p1', 'p3', 'o1'])
      expect(screen.queryByTestId(voiceSendFailedRefusedId(id))).toBeNull();
    // Nothing is undone or resent from here.
    expect(runner.undone).toEqual([]);
    expect(runner.retried).toEqual([]);
  });

  it('without an item lists every refused voice change with its key', async () => {
    await renderWithRunner(<VoiceSendFailedBanner />, { runner: sentLater() });

    expect(screen.getByText('2 voice changes weren’t applied')).toBeOnTheScreen();
    expect(screen.getByTestId(voiceSendFailedRefusedId('p2'))).toHaveTextContent(
      'SHIP-906 · “Approve D10”Claude Code withdrew D10.',
    );
    expect(screen.getByTestId(voiceSendFailedRefusedId('o1'))).toHaveTextContent(
      'SHIP-921 · “State → Active”Priya changed it first.',
    );
  });

  it('Dismiss hides the banner without undoing anything', async () => {
    const runner = sentLater();
    await renderWithRunner(<VoiceSendFailedBanner itemId="item-1" />, { runner });
    await fireEvent.press(screen.getByTestId(voiceSendFailedDismissId));

    expect(screen.queryByTestId(voiceSendFailedRefusedId('p2'))).toBeNull();
    expect(runner.undone).toEqual([]);
  });
});
