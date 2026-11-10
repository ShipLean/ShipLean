import { fireEvent, screen } from '@testing-library/react-native';
import type { AppAction, Executor } from '../../actions/types';
import { aPending, fakeRunner } from '../../test-support/actions';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import { UndoPreviewSheet } from './UndoPreviewSheet';
import {
  undoPreviewConfirmId,
  undoPreviewFailedId,
  undoPreviewLineId,
  undoPreviewRetryId,
} from './UndoPreviewSheet.testIds';
import { undoPartialSheetId, undoPartialStayedId } from './UndoPartialSheet.testIds';

const executor: Executor<AppAction> = {
  area: 'workItem',
  send: async () => undefined,
  describe: (a) => ({ id: a.kind, icon: 'dot', text: `${a.kind} on ${a.key}`, removable: true }),
  reversible: () => true,
};

const part = (id: string, kind: string) =>
  aPending({
    id,
    batchId: 'b1',
    origin: 'voice',
    state: 'sent',
    action: { area: 'workItem', kind, itemId: 'item-1', key: 'SHIP-906' },
  });

const items = [part('p1', 'setState'), part('p2', 'addNote')];

function render(onClose = jest.fn()) {
  const runner = fakeRunner();
  runner.register(executor);
  runner.setPending(items);
  return renderWithRunner(
    <UndoPreviewSheet batchId="b1" itemKey="SHIP-906" items={items} onClose={onClose} />,
    { runner },
  ).then((r) => ({ ...r, onClose }));
}

describe('UndoPreviewSheet', () => {
  it('an undo that can’t reach ShipLean undoes nothing and offers Try again', async () => {
    const { runner, onClose } = await render();
    const undo = jest.spyOn(runner, 'undo').mockResolvedValue([
      { id: 'p2', outcome: 'failed' },
      { id: 'p1', outcome: 'failed' },
    ]);

    await fireEvent.press(screen.getByTestId(undoPreviewConfirmId));

    expect(screen.getByTestId(undoPreviewFailedId)).toHaveTextContent(
      'Couldn’t undo: no connectionNothing was undone. Try again when you’re back online.',
    );
    // Both changes are still listed; nothing says they were undone.
    expect(screen.getByTestId(undoPreviewLineId('p1'))).toBeOnTheScreen();
    expect(screen.getByTestId(undoPreviewLineId('p2'))).toBeOnTheScreen();
    expect(screen.queryByTestId(undoPartialSheetId)).toBeNull();
    expect(onClose).not.toHaveBeenCalled();

    undo.mockResolvedValue([
      { id: 'p2', outcome: 'undone' },
      { id: 'p1', outcome: 'undone' },
    ]);
    await fireEvent.press(screen.getByTestId(undoPreviewRetryId));
    expect(undo.mock.calls).toEqual([['b1'], ['b1']]);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('a part that stayed opens what stayed instead', async () => {
    const { runner } = await render();
    jest.spyOn(runner, 'undo').mockResolvedValue([
      { id: 'p2', outcome: 'undone' },
      { id: 'p1', outcome: 'irreversible' },
    ]);
    await fireEvent.press(screen.getByTestId(undoPreviewConfirmId));

    expect(screen.getByTestId(undoPartialSheetId)).toBeOnTheScreen();
    expect(screen.getByTestId(undoPartialStayedId('p1'))).toBeOnTheScreen();
  });
});
