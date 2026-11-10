import { fireEvent } from '@testing-library/react-native';
import { Pressable, Text } from 'react-native';
import { renderWithRunner } from '../test-support/renderWithRunner';
import { fakeRunner } from '../test-support/actions';
import { useState } from 'react';
import { UNDO } from '../actions/undo';
import { AddPointSheet } from './add/AddPointSheet';
import { DecideSheet } from './decide/DecideSheet';
import { ToDecideView } from './decide/ToDecideView';
import { RetroFeaturesView } from './features/RetroFeaturesView';
import { RetroProvider } from '../shell/providers/retro';
import { RetroActionsProvider, useRetroActions } from './RetroActions';
import { aRetroPoint } from '../test-support/retro';
import { retroIds } from './testIds';

function Tap() {
  const { dispatch } = useRetroActions();
  return (
    <Pressable
      testID="tap"
      onPress={() =>
        dispatch({ area: 'retro', kind: 'support', itemId: null, key: null, pointId: 'pt-1' })
      }
    >
      <Text>+1</Text>
    </Pressable>
  );
}

// Renders the provider again from above, as a parent's state change does.
function Rerenders() {
  const [count, setCount] = useState(0);
  return (
    <RetroProvider projectKey="SHIP">
      <Text testID="count">{count}</Text>
      <Pressable testID="again" onPress={() => setCount((c) => c + 1)} />
    </RetroProvider>
  );
}

describe('the Retro seams', () => {
  it('each stub renders its root id from its props', async () => {
    const point = aRetroPoint();
    const noop = () => undefined;
    const add = await renderWithRunner(
      <>
        <AddPointSheet projectId="p" sprintId="s" open onClose={noop} category={null} />
        <DecideSheet point={point} projectId="p" open onClose={noop} />
        <ToDecideView projectId="p" sprintId="s" />
        <RetroFeaturesView projectId="p" />
      </>,
    );
    for (const id of [
      retroIds.addSheet,
      retroIds.decideSheet,
      retroIds.toDecide,
      retroIds.features,
    ]) {
      expect(add.getByTestId(id)).toBeTruthy();
    }
    await add.rerender(
      <>
        <AddPointSheet projectId="p" sprintId="s" open={false} onClose={noop} category={null} />
        <DecideSheet point={point} projectId="p" open={false} onClose={noop} />
      </>,
    );
    expect(add.queryByTestId(retroIds.addSheet)).toBeNull();
    expect(add.queryByTestId(retroIds.decideSheet)).toBeNull();
  });

  it('dispatches one tap action through the runner with the tap window', async () => {
    const runner = fakeRunner();
    const screen = await renderWithRunner(
      <RetroActionsProvider>
        <Tap />
      </RetroActionsProvider>,
      { runner },
    );
    await fireEvent.press(screen.getByTestId('tap'));
    expect(runner.dispatched).toHaveLength(1);
    expect(runner.dispatched[0].batch).toEqual([
      { area: 'retro', kind: 'support', itemId: null, key: null, pointId: 'pt-1' },
    ]);
    expect(runner.dispatched[0].opts).toEqual({
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });
  });

  it('registers the executor once', async () => {
    const runner = fakeRunner();
    const screen = await renderWithRunner(<Rerenders />, { runner });
    await fireEvent.press(screen.getByTestId('again'));
    await fireEvent.press(screen.getByTestId('again'));
    expect(screen.getByTestId('count').props.children).toBe(2);
    expect(runner.registered).toHaveLength(1);
    expect(runner.registered[0].area).toBe('retro');
  });
});
