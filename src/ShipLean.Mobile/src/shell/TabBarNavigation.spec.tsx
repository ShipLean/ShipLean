import { fireEvent, screen, waitFor } from '@testing-library/react-native';
import { screenIds, tabIds } from '../screens/testIds';
import { backlogApi } from '../test-support/backlog';
import { aProject } from '../test-support/builders';
import { featuresApi } from '../test-support/features';
import { anAnswered, anInbox, inboxApi } from '../test-support/inbox';
import { renderApp } from '../test-support/renderApp';
import { retroApi } from '../test-support/retro';
import { signedInApi } from '../test-support/signedInApi';

const PROJECT = aProject().id;
const project = `/api/projects/${PROJECT}`;
const opts = { projectId: PROJECT };

// The Inbox the app opens on, beside the tab's own fake server.
const inboxReads = {
  [`GET ${project}/inbox`]: anInbox(opts),
  [`GET ${project}/inbox/answered`]: anAnswered(),
};

const tabs = [
  {
    tab: tabIds.backlog,
    root: screenIds.backlog,
    read: `GET ${project}/backlogs/features`,
    api: () => inboxApi(backlogApi({ [`GET ${project}/members`]: [] }, opts), opts),
  },
  {
    tab: tabIds.features,
    root: screenIds.features,
    read: `GET ${project}/features/overview`,
    api: () => featuresApi(inboxReads, opts),
  },
  {
    tab: tabIds.retro,
    root: screenIds.retro,
    read: `GET ${project}/sprints`,
    api: () => retroApi({ ...signedInApi(), ...inboxReads }, opts),
  },
];

// A tab pressed in the tab bar must reach its screen with the project key, so the screen's first read names the
// project. SHIP-1332: the tab routes the navigator made up for unfocused tabs had no params, so a pressed tab got
// key undefined and its queries ran with projectId ''.
describe('tab bar navigation', () => {
  it.each(tabs)(
    'opens $root from the Inbox with the project key',
    async ({ tab, root, read, api }) => {
      const rendered = await renderApp({ url: '/p/SHIP/inbox', api: api() });
      expect(await screen.findByTestId(screenIds.inbox)).toBeOnTheScreen();

      await fireEvent.press(screen.getByTestId(tab));

      expect(await screen.findByTestId(root)).toBeOnTheScreen();
      await waitFor(() => expect(rendered.api.callsTo(read)).not.toHaveLength(0));
      expect(rendered.api.calls.filter((c) => c.path.startsWith('/api/projects//'))).toEqual([]);
    },
  );
});
