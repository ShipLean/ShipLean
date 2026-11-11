import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
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

const HIDDEN = { includeHiddenElements: true };

describe.each(['light', 'dark'] as const)('Button variants (%s)', (scheme) => {
  it('soft fills with surface-2 and keeps the text colour', async () => {
    await renderScreen(<Button label="Later" variant="soft" onPress={() => {}} testID="b" />, {
      scheme,
    });

    expect(screen.getByTestId('b')).toHaveStyle({
      backgroundColor: themes[scheme].colors.surface2,
    });
    expect(screen.getByText('Later')).toHaveStyle({ color: themes[scheme].colors.text });
  });

  it('link is text only in the accent colour', async () => {
    await renderScreen(<Button label="Skip" variant="link" onPress={() => {}} testID="b" />, {
      scheme,
    });

    expect(screen.getByTestId('b')).toHaveStyle({ backgroundColor: 'transparent' });
    expect(screen.getByText('Skip')).toHaveStyle({ color: themes[scheme].colors.accent });
  });

  it('shows a leading icon in the label colour, and none without icon', async () => {
    await renderScreen(
      <Button label="Open" variant="soft" icon="inbox" onPress={() => {}} testID="b" />,
      { scheme },
    );

    expect(screen.getByTestId('icon-inbox', HIDDEN).props.stroke).toBe(themes[scheme].colors.text);
  });

  it('sm is 36 high', async () => {
    await renderScreen(<Button label="Go" size="sm" onPress={() => {}} testID="b" />, { scheme });

    expect(screen.getByTestId('b')).toHaveStyle({ minHeight: 36 });
  });
});

it('draws no icon unless asked', async () => {
  await renderScreen(<Button label="Go" onPress={() => {}} testID="b" />);

  expect(screen.queryByTestId('icon-inbox', HIDDEN)).toBeNull();
});
