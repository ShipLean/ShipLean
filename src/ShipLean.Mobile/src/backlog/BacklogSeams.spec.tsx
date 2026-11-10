import { screen, fireEvent } from '@testing-library/react-native';
import { useState } from 'react';
import { Text, Pressable } from 'react-native';
import { fakeRunner } from '../test-support/actions';
import { aBacklogRow } from '../test-support/backlog';
import { renderWithRunner } from '../test-support/renderWithRunner';
import { UNDO } from '../actions/undo';
import type { BacklogAction } from './actions';
import { BacklogActionsProvider, useBacklogActions } from './BacklogActions';
import { AddRow } from './add/AddRow';
import { BacklogRow } from './list/BacklogRow';
import { MoveSheet } from './move/MoveSheet';
import { addRowId, backlogRowId, moveSheetId } from './testIds';

const note: BacklogAction = {
  area: 'backlog',
  kind: 'addNote',
  itemId: 'i1',
  key: 'SHIP-1',
  body: 'hi',
};

function Tapper() {
  const { dispatch } = useBacklogActions();
  return (
    <Pressable testID="tap" onPress={() => dispatch(note)}>
      <Text>tap</Text>
    </Pressable>
  );
}

// Renders the provider again with new children, the way a parent's update does.
function Host() {
  const [n, setN] = useState(0);
  return (
    <BacklogActionsProvider>
      <Pressable testID="bump" onPress={() => setN(n + 1)}>
        <Text>{n}</Text>
      </Pressable>
    </BacklogActionsProvider>
  );
}

describe('Backlog seams', () => {
  it('each stub renders its root id from its props', async () => {
    const row = aBacklogRow({ key: 'SHIP-906' });
    await renderWithRunner(
      <>
        <BacklogRow
          row={row}
          level="features"
          highlight={null}
          onMove={() => {}}
          onOpen={() => {}}
        />
        <MoveSheet row={row} rows={[row]} level="features" open onClose={() => {}} />
        <AddRow projectId="p" level="stories" parent={{ id: 'f', key: 'SHIP-906' }} />
      </>,
    );
    expect(screen.getByTestId(backlogRowId('SHIP-906'))).toBeTruthy();
    expect(screen.getByTestId(moveSheetId)).toBeTruthy();
    expect(screen.getByTestId(addRowId)).toBeTruthy();
  });

  it('dispatches one tap action through the runner with the tap window', async () => {
    const runner = fakeRunner();
    await renderWithRunner(
      <BacklogActionsProvider>
        <Tapper />
      </BacklogActionsProvider>,
      { runner },
    );
    await fireEvent.press(screen.getByTestId('tap'));
    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0].batch).toEqual([note]);
    expect(runner.dispatched[0].opts).toEqual({
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });
  });

  it('registers the executor once', async () => {
    const runner = fakeRunner();
    await renderWithRunner(<Host />, { runner });
    await fireEvent.press(screen.getByTestId('bump'));
    await fireEvent.press(screen.getByTestId('bump'));
    expect(runner.registered).toHaveLength(1);
    expect(runner.registered[0].area).toBe('backlog');
  });
});
