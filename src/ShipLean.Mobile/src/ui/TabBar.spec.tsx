import { fireEvent, screen } from '@testing-library/react-native';
import { Text } from 'react-native';
import { renderScreen } from '../test-support/renderScreen';
import { themes } from '../theme';
import { TabBar, type TabBarTab } from './TabBar';
import { primitiveIds, tabBadgeId, tabId } from './testIds';

const tabs = (inboxBadge?: number): TabBarTab[] => [
  { id: 'inbox', label: 'Inbox', badge: inboxBadge },
  { id: 'backlog', label: 'Backlog' },
  { id: 'features', label: 'Features' },
  { id: 'retro', label: 'Retro' },
];

describe.each(['light', 'dark'] as const)('TabBar (%s)', (scheme) => {
  it('gives every tab its tab-<id> id and a 44pt target', async () => {
    await renderScreen(<TabBar tabs={tabs()} active="inbox" onPress={() => {}} center={null} />, {
      scheme,
    });

    for (const id of ['inbox', 'backlog', 'features', 'retro']) {
      expect(screen.getByTestId(tabId(id))).toHaveStyle({ minHeight: 44 });
    }
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
    const order = bar.children.map((c) => (typeof c === 'string' ? c : c.props.testID));
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
