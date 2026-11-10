import { fireEvent, screen } from '@testing-library/react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { TabRootBar } from './TabRootBar';
import { primitiveIds } from './testIds';

describe.each(['light', 'dark'] as const)('TabRootBar (%s)', (scheme) => {
  it('calls onTilePress when the key tile is pressed', async () => {
    const onTilePress = jest.fn();
    await renderScreen(
      <TabRootBar projectKey="SHIP" title="Inbox" subtitle="ShipLean" onTilePress={onTilePress} />,
      { scheme },
    );

    await fireEvent.press(screen.getByTestId(primitiveIds.tabRootTile));

    expect(onTilePress).toHaveBeenCalledTimes(1);
  });

  it('gives the tile a 44pt target and paints the bar with the theme surface', async () => {
    await renderScreen(<TabRootBar projectKey="SHIP" title="Inbox" onTilePress={() => {}} />, {
      scheme,
    });

    expect(screen.getByTestId(primitiveIds.tabRootTile)).toHaveStyle({
      minWidth: 44,
      minHeight: 44,
    });
    expect(screen.getByTestId(primitiveIds.tabRootBar)).toHaveStyle({
      backgroundColor: themes[scheme].colors.surface,
    });
  });

  it('sets the key in the theme monospace font', async () => {
    await renderScreen(<TabRootBar projectKey="SHIP" title="Inbox" onTilePress={() => {}} />, {
      scheme,
    });

    expect(screen.getByText('SHIP')).toHaveStyle({ fontFamily: themes[scheme].font.mono });
  });
});
