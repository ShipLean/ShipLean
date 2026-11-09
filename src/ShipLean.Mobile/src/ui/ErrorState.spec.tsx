import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { ErrorState } from './ErrorState';

describe.each(['light', 'dark'] as const)('ErrorState (%s)', (scheme) => {
  test('render_WhenShown_ShouldShowItsTestId', async () => {
    await renderScreen(<ErrorState title="Oops" />, { scheme });

    expect(screen.getByTestId('error-state')).toBeOnTheScreen();
    expect(screen.queryByTestId('error-state-action')).toBeNull();
  });

  test('press_WhenRetryIsPressed_ShouldCallTheAction', async () => {
    const onPress = jest.fn();
    await renderScreen(<ErrorState title="Oops" action={{ label: 'Retry', onPress }} />, {
      scheme,
    });

    await fireEvent.press(screen.getByTestId('error-state-action'));

    expect(onPress).toHaveBeenCalledTimes(1);
  });
});
