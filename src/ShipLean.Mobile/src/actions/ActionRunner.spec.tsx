import { act, fireEvent, render, screen } from '@testing-library/react-native';
import { Pressable, Text } from 'react-native';
import { renderWithRunner } from '../test-support/renderWithRunner';
import { useActionRunner } from './ActionRunnerProvider';
import { InMemoryActionRunner } from './InMemoryActionRunner';
import type { ActionRunner, AppAction, Executor } from './types';
import { UNDO } from './undo';
import { useUndoToast } from './useUndoToast';

const T0 = Date.parse('2030-01-01T09:00:00.000Z');
const now = () => new Date(T0);

function RunnerProbe({ seen }: { seen: (r: ActionRunner) => void }) {
  seen(useActionRunner());
  return null;
}

function ToastProbe({ at = now }: { at?: () => Date }) {
  const toast = useUndoToast(at);
  if (toast === null) return <Text testID="no-toast">none</Text>;
  return (
    <Pressable testID="toast-undo" onPress={() => void toast.undo()}>
      <Text testID="toast-text">{toast.text}</Text>
    </Pressable>
  );
}

const executor: Executor<AppAction> = {
  area: 'workItem',
  send: async () => undefined,
  describe: (a) => ({
    id: a.itemId ?? '',
    icon: 'state',
    text: `${a.key} → ${a.kind}`,
    removable: true,
  }),
  reversible: () => true,
};

const action = (key: string, kind: string): AppAction => ({
  area: 'workItem',
  kind,
  itemId: key,
  key,
});

describe('ActionRunnerProvider', () => {
  it('useActionRunner returns the provider runner and throws outside one', async () => {
    let seen: ActionRunner | null = null;
    const { runner } = await renderWithRunner(<RunnerProbe seen={(r) => (seen = r)} />);
    expect(seen).toBe(runner);

    const quiet = jest.spyOn(console, 'error').mockImplementation(() => undefined);
    await expect(render(<RunnerProbe seen={() => undefined} />)).rejects.toThrow(
      /needs an ActionRunnerProvider/,
    );
    quiet.mockRestore();
  });

  it('useUndoToast words the newest open batch and undo() undoes it', async () => {
    const runner = new InMemoryActionRunner({ now });
    runner.register(executor);
    runner.dispatch([action('SHIP-1', 'Active')], {
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });
    await renderWithRunner(<ToastProbe />, { runner });
    expect(screen.getByTestId('toast-text')).toHaveTextContent('SHIP-1 → Active');

    let newest = '';
    await act(async () => {
      newest = runner.dispatch([action('SHIP-2', 'Done'), action('SHIP-3', 'Done')], {
        undo: UNDO.voice,
        origin: 'voice',
        allOrNothing: true,
      });
    });
    expect(screen.getByTestId('toast-text')).toHaveTextContent('SHIP-2 → Done · SHIP-3 → Done');

    await act(async () => {
      fireEvent.press(screen.getByTestId('toast-undo'));
    });
    expect(runner.pending().some((p) => p.batchId === newest)).toBe(false);
    expect(screen.getByTestId('toast-text')).toHaveTextContent('SHIP-1 → Active');
  });

  it('useUndoToast shows nothing once every window has closed', async () => {
    const runner = new InMemoryActionRunner({ now });
    runner.register(executor);
    runner.dispatch([action('SHIP-1', 'Active')], {
      undo: UNDO.tap,
      origin: 'tap',
      allOrNothing: true,
    });
    runner.dispatch([action('SHIP-2', 'Done')], {
      undo: UNDO.voice,
      origin: 'voice',
      allOrNothing: true,
    });

    await renderWithRunner(<ToastProbe at={() => new Date(T0 + UNDO.voice.ms)} />, { runner });

    expect(screen.getByTestId('no-toast')).toBeOnTheScreen();
  });
});
