import { fireEvent, screen } from '@testing-library/react-native';
import type { AppAction, Executor } from '../../actions/types';
import { aPending, fakeRunner } from '../../test-support/actions';
import { fakeApi } from '../../test-support/fakeApi';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import { aDecision, anItemAsks } from '../../test-support/workItem';
import { UndoPartialSheet } from './UndoPartialSheet';
import {
  decisionChangeOptionId,
  decisionChangePostedId,
  decisionChangeSubmitId,
  undoPartialChangeId,
  undoPartialRetryId,
  undoPartialStayedId,
  undoPartialUndoneId,
} from './UndoPartialSheet.testIds';

const TEXT: Record<string, string> = {
  setState: 'State → Active',
  approve: 'Approve D10',
  addNote: 'Your note',
};

const executor: Executor<AppAction> = {
  area: 'workItem',
  send: async () => undefined,
  describe: (a) => ({ id: a.kind, icon: 'dot', text: TEXT[a.kind] ?? a.kind, removable: true }),
  reversible: (kind) => kind !== 'approve',
};

const part = (id: string, kind: string, extra: object = {}) =>
  aPending({
    id,
    batchId: 'b1',
    origin: 'voice',
    state: 'sent',
    action: { area: 'workItem', kind, itemId: 'item-1', key: 'SHIP-906', ...extra } as AppAction,
  });

const items = [
  part('p1', 'setState'),
  part('p2', 'approve', { numbers: [10] }),
  part('p3', 'addNote'),
];

function runner() {
  const r = fakeRunner();
  r.register(executor);
  return r;
}

describe('UndoPartialSheet', () => {
  it('lists what was undone and what stayed, and Change D10 posts the decision change', async () => {
    const api = fakeApi({
      'GET /api/work-items/item-1/asks': anItemAsks('item-1', {
        decisions: [
          aDecision({
            number: 10,
            label: 'D10',
            question: 'Download the models on mobile data?',
            recommended: 'Wi-Fi only',
            alternatives: ['Ask before mobile data', 'Any connection'],
            currentAnswer: 'Wi-Fi only',
            state: 'confirmed',
          }),
        ],
      }),
      'POST /api/work-items/item-1/decisions/10/change': { status: 204 },
    });
    const outcomes = [
      { id: 'p3', outcome: 'undone' as const },
      { id: 'p2', outcome: 'irreversible' as const },
      { id: 'p1', outcome: 'undone' as const },
    ];
    await renderWithRunner(
      <UndoPartialSheet items={items} outcomes={outcomes} onClose={() => {}} />,
      {
        runner: runner(),
      },
    );

    expect(screen.getByText('Undid 2 of 3')).toBeOnTheScreen();
    expect(screen.getByTestId(undoPartialUndoneId('p1'))).toHaveTextContent('State → Active');
    expect(screen.getByTestId(undoPartialUndoneId('p3'))).toHaveTextContent('Your note');
    expect(screen.getByTestId(undoPartialStayedId('p2'))).toHaveTextContent(
      'Approve D10An approval can’t be taken back here.',
    );
    expect(screen.queryByTestId(undoPartialStayedId('p1'))).toBeNull();
    expect(screen.queryByTestId(undoPartialRetryId)).toBeNull();

    await fireEvent.press(screen.getByTestId(undoPartialChangeId(10)));
    await fireEvent.press(await screen.findByTestId(decisionChangeOptionId('0')));
    await fireEvent.press(screen.getByTestId(decisionChangeSubmitId));

    expect(await screen.findByTestId(decisionChangePostedId)).toHaveTextContent(
      'Posted on SHIP-906; the next agent to pick it up reads it.',
    );
    const posts = api.callsTo('POST /api/work-items/item-1/decisions/10/change');
    expect(posts.map((c) => c.body)).toEqual([
      { kind: 'alternative', alternativeIndex: 0, text: null, note: null },
    ]);
  });

  it('offers Try again when an outcome failed, and no Change for it', async () => {
    const onRetry = jest.fn();
    const outcomes = [
      { id: 'p3', outcome: 'undone' as const },
      { id: 'p2', outcome: 'failed' as const },
      { id: 'p1', outcome: 'undone' as const },
    ];
    await renderWithRunner(
      <UndoPartialSheet items={items} outcomes={outcomes} onRetry={onRetry} onClose={() => {}} />,
      { runner: runner() },
    );

    expect(screen.getByTestId(undoPartialStayedId('p2'))).toHaveTextContent(
      'Approve D10Couldn’t reach ShipLean, so this one wasn’t undone.',
    );
    expect(screen.queryByTestId(undoPartialChangeId(10))).toBeNull();
    await fireEvent.press(screen.getByTestId(undoPartialRetryId));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});
