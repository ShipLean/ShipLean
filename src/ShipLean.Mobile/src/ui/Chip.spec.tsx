import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { Chip } from './Chip';
import { countId } from './testIds';

describe.each(['light', 'dark'] as const)('Chip (%s)', (scheme) => {
  it('fills a selected chip with the accent soft colour and marks it selected', async () => {
    await renderScreen(
      <Chip label="Needs me" count={3} selected onPress={() => {}} testID="needs" />,
      {
        scheme,
      },
    );

    expect(screen.getByTestId('needs')).toBeSelected();
    expect(screen.getByTestId('needs')).toHaveStyle({
      backgroundColor: themes[scheme].colors.accentSoft,
    });
    expect(screen.getByTestId(countId('needs'))).toHaveTextContent('3');
  });

  it('leaves an unselected chip on the surface and calls onPress', async () => {
    const onPress = jest.fn();
    await renderScreen(<Chip label="Open" selected={false} onPress={onPress} testID="open" />, {
      scheme,
    });

    await fireEvent.press(screen.getByTestId('open'));

    expect(onPress).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId('open')).not.toBeSelected();
    expect(screen.getByTestId('open')).toHaveStyle({
      backgroundColor: themes[scheme].colors.surface,
    });
    expect(screen.queryByTestId(countId('open'))).toBeNull();
  });
});
