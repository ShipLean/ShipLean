import { act, fireEvent, screen } from '@testing-library/react-native';
import { Pressable } from 'react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { ToastHost, useToast, type ToastOptions } from './Toast';
import { primitiveIds } from './testIds';

function Trigger({ options, testID = 'show' }: { options: ToastOptions; testID?: string }) {
  const toast = useToast();
  return <Pressable testID={testID} onPress={() => toast.show(options)} />;
}

describe.each(['light', 'dark'] as const)('Toast (%s)', (scheme) => {
  beforeEach(() => jest.useFakeTimers());
  afterEach(() => jest.useRealTimers());

  it('runs the action and closes when the action is pressed', async () => {
    const onPress = jest.fn();
    await renderScreen(
      <ToastHost>
        <Trigger options={{ text: 'Moved', action: { label: 'Undo', onPress }, ms: 5000 }} />
      </ToastHost>,
      { scheme },
    );

    await fireEvent.press(screen.getByTestId('show'));
    await fireEvent.press(screen.getByTestId(primitiveIds.toastAction));

    expect(onPress).toHaveBeenCalledTimes(1);
    expect(screen.queryByTestId(primitiveIds.toast)).toBeNull();
  });

  it('closes after ms and not before', async () => {
    await renderScreen(
      <ToastHost>
        <Trigger options={{ text: 'Saved', ms: 3000 }} />
      </ToastHost>,
      { scheme },
    );

    await fireEvent.press(screen.getByTestId('show'));
    expect(screen.getByTestId(primitiveIds.toast)).toHaveStyle({
      backgroundColor: themes[scheme].colors.text,
    });
    expect(screen.queryByTestId(primitiveIds.toastAction)).toBeNull();

    await act(() => jest.advanceTimersByTime(2999));
    expect(screen.getByTestId(primitiveIds.toast)).toBeOnTheScreen();

    await act(() => jest.advanceTimersByTime(1));
    expect(screen.queryByTestId(primitiveIds.toast)).toBeNull();
  });

  it('shows one toast at a time: a new one replaces the old and restarts the timer', async () => {
    await renderScreen(
      <ToastHost>
        <Trigger options={{ text: 'First', ms: 3000 }} testID="first" />
        <Trigger options={{ text: 'Second', ms: 3000 }} testID="second" />
      </ToastHost>,
      { scheme },
    );

    await fireEvent.press(screen.getByTestId('first'));
    await act(() => jest.advanceTimersByTime(2000));
    await fireEvent.press(screen.getByTestId('second'));

    expect(screen.getAllByTestId(primitiveIds.toast)).toHaveLength(1);
    expect(screen.getByTestId(primitiveIds.toast)).toHaveTextContent('Second');
    await act(() => jest.advanceTimersByTime(2000));
    expect(screen.getByTestId(primitiveIds.toast)).toBeOnTheScreen();
  });
});
