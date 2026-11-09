import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { EmptyState } from './EmptyState';

describe.each(['light', 'dark'] as const)('EmptyState (%s)', (scheme) => {
  test('render_WhenShown_ShouldShowItsTestId', async () => {
    await renderScreen(<EmptyState title="Nothing yet" body="Add one" />, { scheme });

    expect(screen.getByTestId('empty-state')).toBeOnTheScreen();
    expect(screen.queryByTestId('empty-state-action')).toBeNull();
  });

  test('press_WhenTheActionIsPressed_ShouldCallIt', async () => {
    const onPress = jest.fn();
    await renderScreen(<EmptyState title="Nothing" action={{ label: 'Add', onPress }} />, {
      scheme,
    });

    await fireEvent.press(screen.getByTestId('empty-state-action'));

    expect(onPress).toHaveBeenCalledTimes(1);
  });
});
