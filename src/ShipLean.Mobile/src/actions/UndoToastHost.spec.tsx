import { act, fireEvent, screen } from '@testing-library/react-native';
import { aPending, fakeRunner } from '../test-support/actions';
import { renderWithRunner } from '../test-support/renderWithRunner';
import { primitiveIds } from '../ui/testIds';
import {
  undoPartialSheetId,
  undoPartialStayedId,
  undoPartialUndoneId,
} from '../voice/after/UndoPartialSheet.testIds';
import { UndoToastHost } from './UndoToastHost';
import { undoneToastId, undoToastId } from './UndoToastHost.testIds';

const NOW = new Date('2030-01-01T09:00:00.000Z');
const UNTIL = new Date(NOW.getTime() + 8000).toISOString();

const part = (id: string, kind: string) =>
  aPending({
    id,
    batchId: 'b1',
    origin: 'typed',
    state: 'sent',
    createdAt: NOW.toISOString(),
    undoUntil: UNTIL,
    action: { area: 'workItem', kind, itemId: 'item-1', key: 'SHIP-906' },
  });

function runnerWithBatch() {
  const runner = fakeRunner();
  runner.setPending([part('p1', 'setState'), part('p2', 'approve')]);
  return runner;
}

beforeEach(() => {
  jest.useFakeTimers({ now: NOW });
});

afterEach(() => {
  jest.useRealTimers();
});

describe('UndoToastHost', () => {
  it('Undo with every outcome undone says Undone; with an irreversible one opens the partial sheet', async () => {
    const runner = runnerWithBatch();
    const view = await renderWithRunner(<UndoToastHost />, { runner });
    await fireEvent.press(screen.getByTestId(primitiveIds.toastAction));

    expect(runner.undone).toEqual(['b1']);
    expect(screen.queryByTestId(undoToastId)).toBeNull();
    expect(screen.getByTestId(undoneToastId)).toHaveTextContent('Undone · SHIP-906 is as it was');
    expect(screen.queryByTestId(undoPartialSheetId)).toBeNull();
    await act(async () => {
      jest.advanceTimersByTime(4000);
    });
    expect(screen.queryByTestId(undoneToastId)).toBeNull();
    await view.unmount();

    const partly = runnerWithBatch();
    jest.spyOn(partly, 'undo').mockResolvedValue([
      { id: 'p2', outcome: 'irreversible' },
      { id: 'p1', outcome: 'undone' },
    ]);
    await renderWithRunner(<UndoToastHost />, { runner: partly });
    await fireEvent.press(screen.getByTestId(primitiveIds.toastAction));

    expect(screen.queryByTestId(undoneToastId)).toBeNull();
    expect(screen.getByTestId(undoPartialSheetId)).toBeOnTheScreen();
    expect(screen.getByTestId(undoPartialUndoneId('p1'))).toHaveTextContent('setState');
    expect(screen.getByTestId(undoPartialStayedId('p2'))).toHaveTextContent(
      'approveAn approval can’t be taken back here.',
    );
  });
});
