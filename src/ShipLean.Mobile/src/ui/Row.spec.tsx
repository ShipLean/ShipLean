import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { Row } from './Row';

describe.each(['light', 'dark'] as const)('Row (%s)', (scheme) => {
  test('press_WhenOnPressIsGiven_ShouldCallIt', async () => {
    const onPress = jest.fn();
    await renderScreen(<Row title="Alpha" subtitle="2h ago" onPress={onPress} testID="row" />, {
      scheme,
    });

    await fireEvent.press(screen.getByTestId('row'));

    expect(onPress).toHaveBeenCalledTimes(1);
  });

  test('render_WhenThereIsNoOnPress_ShouldShowTheRowWithoutAButtonRole', async () => {
    await renderScreen(<Row title="Alpha" testID="row" />, { scheme });

    expect(screen.getByTestId('row')).toBeOnTheScreen();
    expect(screen.queryByRole('button')).toBeNull();
  });
});
