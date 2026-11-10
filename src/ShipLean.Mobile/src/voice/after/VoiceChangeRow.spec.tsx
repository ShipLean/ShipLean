import type { RevisionDto } from '@shiplean/core/api/workItems';
import { act, fireEvent, screen } from '@testing-library/react-native';
import type { AppAction, Executor } from '../../actions/types';
import { aPending, fakeRunner } from '../../test-support/actions';
import { aMe } from '../../test-support/builders';
import { fakeApi } from '../../test-support/fakeApi';
import { aUser } from '../../test-support/inbox';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import { aHistory } from '../../test-support/workItem';
import { VoiceChangeRow } from './VoiceChangeRow';
import {
  voiceChangeChangedId,
  voiceChangeId,
  voiceChangeUndoId,
  voiceChangeWaitingId,
} from './VoiceChangeRow.testIds';
import {
  undoPreviewConfirmId,
  undoPreviewLineId,
  undoPreviewSheetId,
} from './UndoPreviewSheet.testIds';

const NOW = new Date('2030-01-01T09:00:00.000Z');
const SENT = new Date(NOW.getTime() - 60_000).toISOString();
const ME = aMe();

const executor: Executor<AppAction> = {
  area: 'workItem',
  send: async () => undefined,
  describe: (a) => ({ id: a.kind, icon: 'dot', text: `${a.kind} on ${a.key}`, removable: true }),
  reversible: () => true,
};

function runnerWith(items: ReturnType<typeof aPending>[]) {
  const runner = fakeRunner();
  runner.register(executor);
  runner.setPending(items);
  return runner;
}

const voice = (id: string, state: 'sent' | 'queued', kind = 'setState') =>
  aPending({
    id,
    batchId: 'b1',
    origin: 'voice',
    state,
    createdAt: SENT,
    action: { area: 'workItem', kind, itemId: 'item-1', key: 'SHIP-906' },
  });

function aRevision(overrides: Partial<RevisionDto>): RevisionDto {
  return {
    rev: 7,
    changedBy: {
      id: ME.id,
      name: ME.name,
      initials: 'JD',
      avatarColour: '#2f7fd8',
      photoUrl: null,
    },
    changedAt: SENT,
    changedAtLabel: '9:00',
    origin: 'user',
    changes: [],
    agentName: null,
    webhook: null,
    ...overrides,
  };
}

const serverWith = (revisions: RevisionDto[]) =>
  fakeApi({
    'GET /api/me': ME,
    'GET /api/work-items/item-1/history': aHistory({ revisions, asks: [] }),
  });

beforeEach(() => {
  jest.useFakeTimers({ now: NOW });
});

afterEach(() => {
  jest.useRealTimers();
});

describe('VoiceChangeRow', () => {
  it('Undo opens the undo preview and only Undo there calls runner.undo with the batch id', async () => {
    serverWith([aRevision({})]);
    const runner = runnerWith([voice('p1', 'sent'), voice('p2', 'sent', 'addNote')]);
    await renderWithRunner(<VoiceChangeRow itemId="item-1" itemKey="SHIP-906" />, { runner });

    await fireEvent.press(await screen.findByTestId(voiceChangeUndoId('b1')));
    expect(screen.getByTestId(undoPreviewSheetId)).toBeOnTheScreen();
    expect(screen.getByTestId(undoPreviewLineId('p1'))).toHaveTextContent('setState on SHIP-906');
    expect(screen.getByTestId(undoPreviewLineId('p2'))).toHaveTextContent('addNote on SHIP-906');
    expect(runner.undone).toEqual([]);

    await fireEvent.press(screen.getByTestId(undoPreviewConfirmId));
    expect(runner.undone).toEqual(['b1']);
  });

  it('a queued voice batch shows Waiting to send and Undo removes it without a request', async () => {
    const api = serverWith([]);
    const runner = runnerWith([voice('p1', 'queued')]);
    await renderWithRunner(<VoiceChangeRow itemId="item-1" itemKey="SHIP-906" />, { runner });

    expect(screen.getByTestId(voiceChangeWaitingId('b1'))).toHaveTextContent('Waiting to send');
    await fireEvent.press(screen.getByTestId(voiceChangeUndoId('b1')));

    expect(runner.undone).toEqual(['b1']);
    expect(screen.queryByTestId(undoPreviewSheetId)).toBeNull();
    expect(screen.queryByTestId(voiceChangeId('b1'))).toBeNull();
    expect(api.calls).toEqual([]);
  });

  it('loses Undo when the 10 minutes end', async () => {
    serverWith([aRevision({})]);
    const runner = runnerWith([voice('p1', 'sent')]);
    await renderWithRunner(<VoiceChangeRow itemId="item-1" itemKey="SHIP-906" />, { runner });
    expect(await screen.findByTestId(voiceChangeUndoId('b1'))).toBeOnTheScreen();

    await act(async () => {
      jest.advanceTimersByTime(9 * 60_000);
    });
    expect(screen.queryByTestId(voiceChangeUndoId('b1'))).toBeNull();
    expect(screen.queryByTestId(voiceChangeId('b1'))).toBeNull();
  });

  it('says someone else changed it, with no Undo, once another person changed the item', async () => {
    const priya = aUser();
    serverWith([aRevision({ rev: 8, changedBy: priya, changedAt: NOW.toISOString() })]);
    const runner = runnerWith([voice('p1', 'sent')]);
    await renderWithRunner(<VoiceChangeRow itemId="item-1" itemKey="SHIP-906" />, { runner });

    expect(await screen.findByTestId(voiceChangeChangedId('b1'))).toBeOnTheScreen();
    expect(screen.queryByTestId(voiceChangeUndoId('b1'))).toBeNull();
  });
});
