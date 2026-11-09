import { Text } from 'react-native';
import { screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { Screen } from './Screen';
import { Sheet } from './Sheet';
import { TopBar } from './TopBar';

describe.each(['light', 'dark'] as const)('Screen, TopBar and Sheet (%s)', (scheme) => {
  test('render_WhenScreenHoldsATopBar_ShouldShowRootBarAndTitle', async () => {
    await renderScreen(
      <Screen testID="root">
        <TopBar title="Projects" right={<Text>R</Text>} />
      </Screen>,
      { scheme },
    );

    expect(screen.getByTestId('root')).toBeOnTheScreen();
    expect(screen.getByTestId('top-bar')).toBeOnTheScreen();
    expect(screen.getByTestId('top-bar-title')).toHaveTextContent('Projects');
  });

  test('render_WhenSheetIsShown_ShouldShowRootAndTitle', async () => {
    await renderScreen(
      <Sheet title="Account" testID="sheet">
        <Text>Body</Text>
      </Sheet>,
      { scheme },
    );

    expect(screen.getByTestId('sheet')).toBeOnTheScreen();
    expect(screen.getByTestId('sheet-title')).toHaveTextContent('Account');
  });
});
