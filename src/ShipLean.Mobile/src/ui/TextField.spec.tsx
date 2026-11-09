import { screen } from '@testing-library/react-native';
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
  });
});
