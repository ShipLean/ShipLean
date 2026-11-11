import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { Chip } from './Chip';
import { tint } from './layout';
import { countId, dotId } from './testIds';

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

describe.each(['light', 'dark'] as const)('Chip extras (%s)', (scheme) => {
  it('ask tone takes the tan fill and no border', async () => {
    await renderScreen(
      <Chip label="Needs me" tone="ask" selected={false} onPress={() => {}} testID="c" />,
      { scheme },
    );

    expect(screen.getByTestId('c')).toHaveStyle({
      backgroundColor: tint(themes[scheme].colors.warn, 0.14),
      borderColor: 'transparent',
    });
  });

  it('shows a colour dot, a leading icon and a trailing chevron when asked', async () => {
    await renderScreen(
      <Chip
        label="Open"
        dot="#123456"
        icon="inbox"
        trailing="chevron"
        selected={false}
        onPress={() => {}}
        testID="c"
      />,
      { scheme },
    );

    expect(screen.getByTestId(dotId('c'))).toHaveStyle({ backgroundColor: '#123456' });
    expect(screen.getByTestId('icon-inbox', { includeHiddenElements: true })).toBeTruthy();
    expect(screen.getByTestId('icon-chevron', { includeHiddenElements: true })).toBeTruthy();
  });

  it('draws none of them by default', async () => {
    await renderScreen(<Chip label="Open" selected={false} onPress={() => {}} testID="c" />, {
      scheme,
    });

    expect(screen.queryByTestId(dotId('c'))).toBeNull();
    expect(screen.queryByTestId('icon-down', { includeHiddenElements: true })).toBeNull();
    expect(screen.queryByTestId('icon-chevron', { includeHiddenElements: true })).toBeNull();
  });

  it('trailing down draws the down arrow', async () => {
    await renderScreen(
      <Chip label="All epics" trailing="down" selected={false} onPress={() => {}} testID="c" />,
      { scheme },
    );

    expect(screen.getByTestId('icon-down', { includeHiddenElements: true })).toBeTruthy();
    expect(screen.queryByTestId('icon-chevron', { includeHiddenElements: true })).toBeNull();
  });
});
