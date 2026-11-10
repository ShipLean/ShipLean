import { act, fireEvent, screen } from '@testing-library/react-native';
import { projectRowId } from '../screens/projects/testIds';
import { screenIds, tabIds } from '../screens/testIds';
import { renderApp } from '../test-support/renderApp';

async function openProject(key: string) {
  await renderApp();
  await act(async () => fireEvent.press(await screen.findByTestId(projectRowId(key))));
  return screen.findByTestId(screenIds.inbox);
}

describe('project tabs', () => {
  it('opens the project tabs on screen-inbox when a project row is tapped', async () => {
    expect(await openProject('SHIP')).toBeOnTheScreen();
  });

  it('shows the four tabs and the mic slot in order', async () => {
    await openProject('SHIP');

    const tabs = screen.getAllByTestId(/^tab-(inbox|backlog|mic|features|retro)$/);
    expect(tabs.map((tab) => tab.props.testID)).toEqual([
      tabIds.inbox,
      tabIds.backlog,
      tabIds.mic,
      tabIds.features,
      tabIds.retro,
    ]);
  });

  it('switches to screen-backlog, screen-features and screen-retro from the tab bar', async () => {
    await openProject('SHIP');

    for (const [tab, root] of [
      [tabIds.backlog, screenIds.backlog],
      [tabIds.features, screenIds.features],
      [tabIds.retro, screenIds.retro],
      [tabIds.inbox, screenIds.inbox],
    ] as const) {
      await act(async () => fireEvent.press(screen.getByTestId(tab)));
      expect(await screen.findByTestId(root)).toBeOnTheScreen();
    }
  });

  it('goes back to screen-projects from the key tile', async () => {
    await openProject('SHIP');

    await act(async () => fireEvent.press(screen.getAllByTestId(tabIds.rootTile)[0]!));

    expect(await screen.findByTestId(screenIds.projects)).toBeOnTheScreen();
    expect(screen.queryByTestId(screenIds.inbox)).toBeNull();
  });
});
