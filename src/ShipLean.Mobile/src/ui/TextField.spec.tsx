import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { TextField } from './TextField';

describe.each(['light', 'dark'] as const)('TextField (%s)', (scheme) => {
  test('render_WhenShown_ShouldShowTheInputByTestId', async () => {
    await renderScreen(<TextField label="Server" value="" onChangeText={() => {}} testID="srv" />, {
      scheme,
    });

    expect(screen.getByTestId('srv')).toBeOnTheScreen();
  });

  test('render_WhenHintOnly_ShouldShowTheHint', async () => {
    await renderScreen(
      <TextField label="Server" value="" onChangeText={() => {}} hint="Its address" testID="srv" />,
      { scheme },
    );

    expect(screen.getByTestId('srv-hint')).toBeOnTheScreen();
    expect(screen.queryByTestId('srv-error')).toBeNull();
    expect(screen.getByTestId('srv').props.accessibilityHint).toBe('Its address');
  });

  test('render_WhenHintAndError_ShouldShowTheErrorInsteadOfTheHint', async () => {
    await renderScreen(
      <TextField
        label="Server"
        value=""
        onChangeText={() => {}}
        hint="Its address"
        error="Not reachable"
        testID="srv"
      />,
      { scheme },
    );

    expect(screen.getByTestId('srv-error')).toBeOnTheScreen();
    expect(screen.queryByTestId('srv-hint')).toBeNull();
    expect(screen.getByTestId('srv').props.accessibilityHint).toBe('Not reachable');
  });

  test('render_WhenTextAndHandler_ShouldShowClearButtonThatCallsIt', async () => {
    const onClear = jest.fn();
    await renderScreen(
      <TextField
        label="Server"
        value="abc"
        onChangeText={() => {}}
        onClear={onClear}
        testID="srv"
      />,
      { scheme },
    );

    await fireEvent.press(screen.getByTestId('srv-clear'));

    expect(onClear).toHaveBeenCalledTimes(1);
  });

  test('render_WhenNoText_ShouldHideClearButton', async () => {
    await renderScreen(
      <TextField label="Server" value="" onChangeText={() => {}} onClear={() => {}} testID="srv" />,
      { scheme },
    );

    expect(screen.queryByTestId('srv-clear')).toBeNull();
  });

  test('render_WhenNoHandler_ShouldHideClearButton', async () => {
    await renderScreen(
      <TextField label="Server" value="abc" onChangeText={() => {}} testID="srv" />,
      { scheme },
    );

    expect(screen.queryByTestId('srv-clear')).toBeNull();
  });
});
