import { fireEvent, screen, within } from '@testing-library/react-native';
import { Text } from 'react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { TabBar, type TabBarTab } from './TabBar';
import { iconId, primitiveIds, tabBadgeId, tabId } from './testIds';

const tabs = (inboxBadge?: number): TabBarTab[] => [
  { id: 'inbox', label: 'Inbox', badge: inboxBadge },
  { id: 'backlog', label: 'Backlog' },
  { id: 'features', label: 'Features' },
  { id: 'retro', label: 'Retro' },
];

// Icons are hidden from screen readers, so the queries look at hidden elements too.
const HIDDEN = { includeHiddenElements: true };

describe.each(['light', 'dark'] as const)('TabBar (%s)', (scheme) => {
  it('gives every tab its tab-<id> id and a 44pt target', async () => {
    await renderScreen(<TabBar tabs={tabs()} active="inbox" onPress={() => {}} center={null} />, {
      scheme,
    });

    for (const id of ['inbox', 'backlog', 'features', 'retro']) {
      expect(screen.getByTestId(tabId(id))).toHaveStyle({ minHeight: 44 });
    }
  });

  it('draws each tab its icon, inbox, list, board and retro, in the active or muted colour', async () => {
    await renderScreen(<TabBar tabs={tabs()} active="backlog" onPress={() => {}} center={null} />, {
      scheme,
    });

    for (const [tab, icon] of [
      ['inbox', 'inbox'],
      ['backlog', 'list'],
      ['features', 'board'],
      ['retro', 'retro'],
    ] as const) {
      expect(
        within(screen.getByTestId(tabId(tab))).getByTestId(iconId(icon), HIDDEN),
      ).toBeOnTheScreen();
    }
    const colours = themes[scheme].colors;
    expect(
      within(screen.getByTestId(tabId('backlog'))).getByTestId(iconId('list'), HIDDEN).props.stroke,
    ).toBe(colours.accent);
    expect(
      within(screen.getByTestId(tabId('inbox'))).getByTestId(iconId('inbox'), HIDDEN).props.stroke,
    ).toBe(colours.muted);
  });

  it('keeps the badge on Inbox beside its icon', async () => {
    await renderScreen(<TabBar tabs={tabs(3)} active="inbox" onPress={() => {}} center={null} />, {
      scheme,
    });

    const inbox = screen.getByTestId(tabId('inbox'));
    expect(within(inbox).getByTestId(iconId('inbox'), HIDDEN)).toBeOnTheScreen();
    expect(within(inbox).getByTestId(tabBadgeId('inbox'))).toHaveTextContent('3');
  });

  it('raises the mic slot above the bar, reaching up by the rise so its touches stay inside the root', async () => {
    await renderScreen(<TabBar tabs={tabs()} active="inbox" onPress={() => {}} center={null} />, {
      scheme,
    });

    expect(screen.getByTestId(primitiveIds.tabBar)).toHaveStyle({
      height: 26 + 56 + 34,
      marginTop: -26,
      paddingTop: 26,
    });
    expect(screen.getByTestId(primitiveIds.tabMic)).toHaveStyle({ marginTop: -26 });
  });

  it('hides a 0 badge', async () => {
    await renderScreen(<TabBar tabs={tabs(0)} active="inbox" onPress={() => {}} center={null} />, {
      scheme,
    });

    expect(screen.queryByTestId(tabBadgeId('inbox'))).toBeNull();
  });

  it('shows a badge of 15', async () => {
    await renderScreen(<TabBar tabs={tabs(15)} active="inbox" onPress={() => {}} center={null} />, {
      scheme,
    });

    expect(screen.getByTestId(tabBadgeId('inbox'))).toHaveTextContent('15');
    expect(screen.getByTestId(tabBadgeId('inbox'))).toHaveStyle({
      backgroundColor: themes[scheme].colors.warn,
    });
  });

  it('calls onPress with the tab pressed and marks the active tab selected', async () => {
    const onPress = jest.fn();
    await renderScreen(<TabBar tabs={tabs()} active="backlog" onPress={onPress} center={null} />, {
      scheme,
    });

    await fireEvent.press(screen.getByTestId(tabId('features')));

    expect(onPress).toHaveBeenCalledWith('features');
    expect(screen.getByTestId(tabId('backlog'))).toBeSelected();
    expect(screen.getByTestId(tabId('inbox'))).not.toBeSelected();
  });

  it('renders center inside tab-mic, between Backlog and Features', async () => {
    await renderScreen(
      <TabBar
        tabs={tabs()}
        active="inbox"
        onPress={() => {}}
        center={<Text testID="the-mic">mic</Text>}
      />,
      { scheme },
    );

    const bar = screen.getByTestId(primitiveIds.tabBar);
    // The bar's surface layer has no id; the tabs and the mic slot are its identified children.
    const order = bar.children
      .map((c) => (typeof c === 'string' ? c : c.props.testID))
      .filter((id) => id !== undefined);
    expect(order).toEqual([
      tabId('inbox'),
      tabId('backlog'),
      primitiveIds.tabMic,
      tabId('features'),
      tabId('retro'),
    ]);
    expect(screen.getByTestId(primitiveIds.tabMic)).toContainElement(screen.getByTestId('the-mic'));
  });
});
