import { fireEvent, screen, within } from '@testing-library/react-native';
import { UNDO } from '../../actions/undo';
import { fakeRunner } from '../../test-support/actions';
import { designFeatures } from '../../test-support/backlog';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import type { Scheme } from '../../theme/themes';
import { optionId } from '../../ui/testIds';
import { BacklogActionsProvider } from '../BacklogActions';
import {
  moveApplyId,
  moveBackId,
  moveFilteredId,
  moveItemId,
  moveOptionId,
  moveOptionToId,
  movePickId,
  movePreviewId,
  movePreviewPosId,
  movePreviewRowId,
  moveSideId,
  moveWarningId,
} from '../testIds';
import { MoveSheet } from './MoveSheet';

const PROJECT = '00000002-0000-4000-8000-000000000001';

async function open(key: string, scheme: Scheme = 'light', filtering = false) {
  const rows = designFeatures();
  const row = rows.find((r) => r.key === key)!;
  const runner = fakeRunner();
  const onClose = jest.fn();
  await renderWithRunner(
    <BacklogActionsProvider>
      <MoveSheet
        projectId={PROJECT}
        row={row}
        rows={rows}
        level="features"
        filtering={filtering}
        open
        onClose={onClose}
      />
    </BacklogActionsProvider>,
    { runner, scheme },
  );
  const id = (k: string) => rows.find((r) => r.key === k)!.id;
  return { rows, row, runner, onClose, id };
}

const text = (testID: string) => screen.getByTestId(testID);

describe.each<Scheme>(['light', 'dark'])('Move sheet (%s)', (scheme) => {
  it('renders the move state with the rank each choice lands on', async () => {
    await open('SHIP-906', scheme);
    expect(text(moveItemId)).toHaveTextContent('SHIP-906 · rank 4 of 7 Features');
    expect(text(moveOptionToId('top'))).toHaveTextContent('to 1');
    expect(text(moveOptionToId('bottom'))).toHaveTextContent('to 7');
    expect(text(moveOptionToId('up'))).toHaveTextContent('to 3');
    expect(text(moveOptionToId('down'))).toHaveTextContent('to 5');
    expect(text(moveOptionId('other'))).toBeTruthy();
  });

  it('lists every shifted row before and after, moved row first', async () => {
    await open('SHIP-906', scheme);
    await fireEvent.press(text(moveOptionId('other')));
    await fireEvent.press(text(movePickId('SHIP-907')));
    const rows = screen.getAllByTestId(/^move-preview-row-/);
    expect(rows.map((r) => r.props.testID)).toEqual([
      movePreviewRowId('SHIP-906'),
      movePreviewRowId('SHIP-907'),
      movePreviewRowId('SHIP-892'),
    ]);
    expect(text(movePreviewPosId('SHIP-906'))).toHaveTextContent('4 → 2');
    expect(text(movePreviewPosId('SHIP-907'))).toHaveTextContent('2 → 3');
    expect(text(movePreviewPosId('SHIP-892'))).toHaveTextContent('3 → 4');
    expect(
      within(text(movePreviewRowId('SHIP-907'))).getByText(/Mobile app foundations/),
    ).toBeTruthy();
  });

  it('renders the preview state and dispatches one move on Apply', async () => {
    const { runner, onClose, id, row } = await open('SHIP-906', scheme);
    await fireEvent.press(text(moveOptionId('up')));
    expect(text(movePreviewId)).toBeTruthy();
    expect(screen.queryByTestId(moveWarningId)).toBeNull();
    expect(runner.dispatched).toHaveLength(0);
    await fireEvent.press(text(moveApplyId));
    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0].batch).toEqual([
      {
        area: 'backlog',
        kind: 'move',
        projectId: PROJECT,
        itemId: row.id,
        key: 'SHIP-906',
        revision: row.revision,
        level: 'features',
        fromRank: 4,
        toRank: 3,
        placement: { slot: 'before', beforeItemId: id('SHIP-892') },
        undoPlacement: { slot: 'before', beforeItemId: id('SHIP-891') },
        shifted: [{ key: 'SHIP-892', from: 3, to: 4 }],
        warning: null,
      },
    ]);
    expect(runner.dispatched[0].opts).toEqual({
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('warns, and still applies, when the move puts the item above its blocker', async () => {
    const { runner } = await open('SHIP-906', scheme);
    await fireEvent.press(text(moveOptionId('top')));
    expect(text(moveWarningId)).toHaveTextContent(/SHIP-906 is blocked by SHIP-907$/);
    await fireEvent.press(text(moveApplyId));
    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0].batch[0]).toMatchObject({
      warning: 'SHIP-906 is blocked by SHIP-907',
    });
  });

  it('picks an item for Above or below', async () => {
    const { runner, id } = await open('SHIP-906', scheme);
    await fireEvent.press(text(moveOptionId('other')));
    // Directly below SHIP-892 is where it already sits.
    await fireEvent.press(text(optionId(moveSideId, 'below')));
    expect(text(movePickId('SHIP-892')).props.accessibilityState.disabled).toBe(true);
    await fireEvent.press(text(movePickId('SHIP-707')));
    expect(text(movePreviewPosId('SHIP-906'))).toHaveTextContent('4 → 2');
    await fireEvent.press(text(moveBackId));
    // Back to the picker, now above.
    await fireEvent.press(text(optionId(moveSideId, 'above')));
    await fireEvent.press(text(movePickId('SHIP-707')));
    expect(text(movePreviewPosId('SHIP-906'))).toHaveTextContent('4 → 1');
    await fireEvent.press(text(moveApplyId));
    expect(runner.dispatched[0].batch[0]).toMatchObject({
      toRank: 1,
      placement: { slot: 'before', beforeItemId: id('SHIP-707') },
    });
  });

  it('turns off a move that changes nothing', async () => {
    await open('SHIP-707', scheme);
    expect(text(moveOptionToId('top'))).toHaveTextContent('Already at the top');
    expect(text(moveOptionToId('up'))).toHaveTextContent('Already at the top');
    expect(text(moveOptionId('top')).props.accessibilityState.disabled).toBe(true);
    expect(text(moveOptionToId('down'))).toHaveTextContent('to 2');
  });

  it('disables moves while a criteria filter is on', async () => {
    await open('SHIP-906', scheme, true);
    expect(text(moveFilteredId)).toHaveTextContent('Clear the filter to move items.');
    expect(screen.queryByTestId(moveOptionId('top'))).toBeNull();
    expect(screen.queryByTestId(moveOptionId('other'))).toBeNull();
  });
});
