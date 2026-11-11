import { act, fireEvent, screen, within } from '@testing-library/react-native';
import { projectRowId } from '../screens/projects/testIds';
import { screenIds, tabIds } from '../screens/testIds';
import { renderApp } from '../test-support/renderApp';
import { renderWithVoice } from '../test-support/voiceUi';
import { Dock } from '../ui/Dock';
import { primitiveIds } from '../ui/testIds';
import { micSlotId } from '../voice/MicSlot.testIds';

jest.mock('../inbox/useInboxBadge', () => ({
  useInboxBadge: (projectKey: string) => (projectKey === 'SHIP' ? 3 : null),
}));

describe('voice mounts', () => {
  it('shows the TabBar with mic-tabBar in the middle and the badge useInboxBadge gives', async () => {
    await renderApp();
    await act(async () => fireEvent.press(await screen.findByTestId(projectRowId('SHIP'))));
    await screen.findByTestId(screenIds.inbox);

    const bar = screen.getByTestId(primitiveIds.tabBar);
    const order = within(bar)
      .getAllByTestId(/^tab-(inbox|backlog|mic|features|retro)$/)
      .map((t) => t.props.testID);
    expect(order).toEqual([
      tabIds.inbox,
      tabIds.backlog,
      tabIds.mic,
      tabIds.features,
      tabIds.retro,
    ]);
    expect(
      within(screen.getByTestId(tabIds.mic)).getByTestId(micSlotId('tabBar')),
    ).toBeOnTheScreen();
    expect(within(screen.getByTestId(tabIds.inboxBadge)).getByText('3')).toBeOnTheScreen();
  });

  it('shows mic-dock in the Dock', async () => {
    await renderWithVoice(<Dock placeholder="Type a note" />);
    expect(
      within(screen.getByTestId(primitiveIds.dockMic)).getByTestId(micSlotId('dock')),
    ).toBeOnTheScreen();
  });
});
