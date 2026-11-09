import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { Button } from './Button';

describe.each(['light', 'dark'] as const)('Button (%s)', (scheme) => {
  test('render_WhenShown_ShouldShowItsTestId', async () => {
    await renderScreen(<Button label="Go" onPress={() => {}} testID="go" />, { scheme });

    expect(screen.getByTestId('go')).toBeOnTheScreen();
  });

  test('press_WhenEnabled_ShouldCallOnPress', async () => {
    const onPress = jest.fn();
    await renderScreen(<Button label="Go" onPress={onPress} testID="go" />, { scheme });

    await fireEvent.press(screen.getByTestId('go'));

    expect(onPress).toHaveBeenCalledTimes(1);
  });

  test('press_WhenDisabled_ShouldNotCallOnPress', async () => {
    const onPress = jest.fn();
    await renderScreen(<Button label="Go" onPress={onPress} disabled testID="go" />, { scheme });

    await fireEvent.press(screen.getByTestId('go'));

    expect(onPress).not.toHaveBeenCalled();
  });

  test('press_WhenBusy_ShouldNotCallOnPress', async () => {
    const onPress = jest.fn();
    await renderScreen(<Button label="Go" onPress={onPress} busy testID="go" />, { scheme });

    await fireEvent.press(screen.getByTestId('go'));

    expect(onPress).not.toHaveBeenCalled();
  });
});
