import type { AnsweredDto, InboxDto } from '@shiplean/core/api/inbox';
import { act, fireEvent, screen } from '@testing-library/react-native';
import { AppState, Text, type AppStateStatus } from 'react-native';
import { InboxActionsProvider } from '../inbox/InboxActions';
import { InboxSheetsProvider } from '../inbox/InboxSheets';
import type { InboxAction } from '../inbox/actions';
import { inboxIds, inboxRowId } from '../inbox/testIds';
import { useInboxBadge } from '../inbox/useInboxBadge';
import { aPending, fakeRunner, type FakeRunner } from '../test-support/actions';
import { aProject } from '../test-support/builders';
import { apiError } from '../test-support/fakeApi';
import { anAnswered, anInbox, inboxApi } from '../test-support/inbox';
import { designInbox } from '../test-support/inboxDesign';
import { renderApp } from '../test-support/renderApp';
import { renderWithRunner } from '../test-support/renderWithRunner';
import type { Scheme } from '../theme/themes';
import { optionId, primitiveIds, tabBadgeId, uiIds } from '../ui/testIds';
import { InboxScreen } from './InboxScreen';
import { screenIds, tabIds } from './testIds';

const project = aProject();
const INBOX_ROUTE = `GET /api/projects/${project.id}/inbox`;

// The badge as the tab bar reads it, next to the screen (the tab bar itself is covered through renderApp below).
function BadgeProbe() {
  return <Text testID="badge-probe">{String(useInboxBadge('SHIP'))}</Text>;
}

async function mount(
  inbox: InboxDto,
  opts: {
    answered?: AnsweredDto;
    api?: ReturnType<typeof inboxApi>;
    runner?: FakeRunner;
    scheme: Scheme;
  },
) {
  const api = opts.api ?? inboxApi({}, { inbox, answered: opts.answered });
  api.install();
  const result = await renderWithRunner(
    <InboxActionsProvider>
      <InboxSheetsProvider>
        <InboxScreen projectKey="SHIP" />
        <BadgeProbe />
      </InboxSheetsProvider>
    </InboxActionsProvider>,
    { runner: opts.runner, scheme: opts.scheme },
  );
  return Object.assign(result, { api });
}

function approve(itemId: string, key: string, numbers: number[]): InboxAction {
  return { area: 'inbox', projectId: project.id, itemId, key, kind: 'approve', numbers };
}

const rowKeys = () =>
  screen
    .getAllByTestId(/^inbox-row-/)
    .map((n) => String(n.props.testID).slice('inbox-row-'.length));

describe.each<Scheme>(['light', 'dark'])('InboxScreen (%s)', (scheme) => {
  const design = () => designInbox(project.id);

  it('lists the design rows in the server order', async () => {
    const inbox = design();
    await mount(inbox, { scheme });
    expect(await screen.findByTestId(inboxRowId('SHIP-743'))).toBeTruthy();
    expect(rowKeys()).toEqual(inbox.items.map((i) => i.key));
    expect(screen.getByText('15 asks on 6 items')).toBeTruthy();
  });

  it('indents a Story under its Feature', async () => {
    await mount(design(), { scheme });
    const story = await screen.findByTestId(inboxRowId('SHIP-924'));
    expect(story.props['data-indented']).toBe(true);
    expect(screen.getByTestId(inboxRowId('SHIP-907')).props['data-indented']).toBe(false);
  });

  it('counts asks in the badge, not items', async () => {
    await mount(design(), { scheme });
    await screen.findByTestId(inboxRowId('SHIP-743'));
    expect(screen.getByTestId('badge-probe').props.children).toBe('15');
  });

  it('drops the badge while an answer is pending', async () => {
    const inbox = design();
    const runner = fakeRunner();
    await mount(inbox, { scheme, runner });
    await screen.findByTestId(inboxRowId('SHIP-743'));
    const item = inbox.items.find((i) => i.key === 'SHIP-907')!;
    const action = approve(item.id, item.key, [1, 2, 3, 4, 5, 6, 7, 8]);
    await act(async () => runner.setPending([aPending({ action, state: 'undo' })]));
    expect(screen.getByTestId('badge-probe').props.children).toBe('7');
    expect(screen.getByText('7 asks on 5 items')).toBeTruthy();
    // Inside the undo window the row stays, for its Undo.
    expect(screen.getByTestId(inboxRowId('SHIP-907'))).toBeTruthy();
    // Past the undo window every ask on SHIP-907 is answered: the row leaves too.
    await act(async () => runner.setPending([aPending({ action, state: 'sent' })]));
    expect(screen.queryByTestId(inboxRowId('SHIP-907'))).toBeNull();
    expect(screen.getByText('7 asks on 5 items')).toBeTruthy();
  });

  it('raises the badge again when the answer fails', async () => {
    const inbox = design();
    const runner = fakeRunner();
    await mount(inbox, { scheme, runner });
    await screen.findByTestId(inboxRowId('SHIP-743'));
    const item = inbox.items.find((i) => i.key === 'SHIP-907')!;
    const action = approve(item.id, item.key, [1, 2, 3, 4, 5, 6, 7, 8]);
    await act(async () => runner.setPending([aPending({ action, state: 'sending' })]));
    expect(screen.getByTestId('badge-probe').props.children).toBe('7');
    await act(async () => runner.setPending([aPending({ action, state: 'failed' })]));
    expect(screen.getByTestId('badge-probe').props.children).toBe('15');
    expect(screen.getByTestId(inboxRowId('SHIP-907'))).toBeTruthy();
  });

  it('shows skeletons on first load with nothing cached', async () => {
    const api = inboxApi({}, { inbox: design() });
    api.hang(INBOX_ROUTE);
    await mount(design(), { scheme, api });
    expect(await screen.findByTestId(primitiveIds.skeleton)).toBeTruthy();
    expect(screen.queryByTestId(uiIds.errorState)).toBeNull();
    expect(screen.getByTestId('badge-probe').props.children).toBe('null');
    await act(async () => api.release(INBOX_ROUTE));
    expect(await screen.findByTestId(inboxRowId('SHIP-743'))).toBeTruthy();
    expect(screen.queryByTestId(primitiveIds.skeleton)).toBeNull();
  });

  it('shows Retry when the first load fails', async () => {
    const api = inboxApi({}, { inbox: design() });
    api.failNext(INBOX_ROUTE, apiError(500, 'server_error'));
    await mount(design(), { scheme, api });
    const retry = await screen.findByTestId(uiIds.errorStateAction);
    await fireEvent.press(retry);
    expect(await screen.findByTestId(inboxRowId('SHIP-743'))).toBeTruthy();
    expect(api.callsTo(INBOX_ROUTE)).toHaveLength(2);
  });

  it('keeps the cached list when a refetch fails', async () => {
    const api = inboxApi({}, { inbox: design() });
    const listeners: ((s: AppStateStatus) => void)[] = [];
    jest.spyOn(AppState, 'addEventListener').mockImplementation((_t, cb) => {
      listeners.push(cb as (s: AppStateStatus) => void);
      return { remove: () => {} };
    });
    await mount(design(), { scheme, api });
    await screen.findByTestId(inboxRowId('SHIP-743'));
    api.failNext(INBOX_ROUTE, apiError(500, 'server_error'));
    await act(async () => listeners.forEach((cb) => cb('active')));
    expect(api.callsTo(INBOX_ROUTE)).toHaveLength(2);
    expect(screen.queryByTestId(uiIds.errorState)).toBeNull();
    expect(screen.getByTestId(inboxRowId('SHIP-743'))).toBeTruthy();
  });

  it("shows today's counts when nothing waits", async () => {
    await mount(anInbox({ items: [], projectId: project.id }), {
      scheme,
      answered: anAnswered({ answeredToday: 6, pickedUpToday: 4 }),
    });
    expect(await screen.findByTestId(inboxIds.empty)).toBeTruthy();
    expect(await screen.findByText('You answered 6 today')).toBeTruthy();
    expect(screen.getByText('Agents have picked up 4 of them')).toBeTruthy();
    expect(screen.queryByTestId(inboxIds.emptyNew)).toBeNull();
    expect(screen.getByText('nothing waiting')).toBeTruthy();
  });

  it('shows the kinds of ask to a person who never had one', async () => {
    await mount(anInbox({ items: [], everWaited: false, projectId: project.id }), { scheme });
    expect(await screen.findByTestId(inboxIds.emptyNew)).toBeTruthy();
    expect(screen.getByTestId(inboxIds.kinds)).toBeTruthy();
    expect(screen.getByText('Decisions to confirm')).toBeTruthy();
    expect(screen.queryByTestId(inboxIds.empty)).toBeNull();
    expect(screen.queryByText(/Notify me/)).toBeNull();
  });

  it('refetches on pull down', async () => {
    const api = inboxApi({}, { inbox: design() });
    await mount(design(), { scheme, api });
    await screen.findByTestId(inboxRowId('SHIP-743'));
    const before = api.callsTo(INBOX_ROUTE).length;
    const { refreshControl } = screen.getByTestId(inboxIds.list).props;
    await act(async () => refreshControl.props.onRefresh());
    expect(api.callsTo(INBOX_ROUTE)).toHaveLength(before + 1);
  });

  it('refetches when the app returns to the foreground', async () => {
    const api = inboxApi({}, { inbox: design() });
    const listeners: ((s: AppStateStatus) => void)[] = [];
    jest.spyOn(AppState, 'addEventListener').mockImplementation((_t, cb) => {
      listeners.push(cb as (s: AppStateStatus) => void);
      return { remove: () => {} };
    });
    await mount(design(), { scheme, api });
    await screen.findByTestId(inboxRowId('SHIP-743'));
    const before = api.callsTo(INBOX_ROUTE).length;
    await act(async () => listeners.forEach((cb) => cb('background')));
    expect(api.callsTo(INBOX_ROUTE)).toHaveLength(before);
    await act(async () => listeners.forEach((cb) => cb('active')));
    expect(api.callsTo(INBOX_ROUTE)).toHaveLength(before + 1);
  });

  it('switches to Answered and mounts the answered list', async () => {
    await mount(design(), { scheme });
    await screen.findByTestId(inboxRowId('SHIP-743'));
    expect(screen.queryByTestId(inboxIds.answeredList)).toBeNull();
    await fireEvent.press(screen.getByTestId(optionId(inboxIds.segment, 'answered')));
    expect(screen.getByTestId(inboxIds.answeredList)).toBeTruthy();
    expect(screen.queryByTestId(inboxRowId('SHIP-743'))).toBeNull();
    await fireEvent.press(screen.getByTestId(optionId(inboxIds.segment, 'waiting')));
    expect(screen.getByTestId(inboxRowId('SHIP-743'))).toBeTruthy();
  });

  it('never asks for a project with an empty id on a cold open', async () => {
    const api = inboxApi({}, { inbox: design() });
    api.hang('GET /api/projects');
    await mount(design(), { scheme, api });
    expect(await screen.findByTestId(primitiveIds.skeleton)).toBeTruthy();
    expect(api.calls.filter((c) => c.path.includes('/projects//'))).toEqual([]);
    expect(api.callsTo(INBOX_ROUTE)).toHaveLength(0);
    await act(async () => api.release('GET /api/projects'));
    expect(await screen.findByTestId(inboxRowId('SHIP-743'))).toBeTruthy();
  });
});

describe('InboxScreen in the app', () => {
  const openInbox = async () => {
    const api = inboxApi({}, { inbox: designInbox(project.id) });
    await renderApp({ url: '/p/SHIP/inbox', api });
    return api;
  };

  it('goes back to Projects from the tile', async () => {
    await openInbox();
    await screen.findByTestId(inboxRowId('SHIP-743'));
    await fireEvent.press(screen.getAllByTestId(tabIds.rootTile)[0]!);
    expect(await screen.findByTestId(screenIds.projects)).toBeOnTheScreen();
  });

  it('opens the Work item screen when a row is tapped', async () => {
    await openInbox();
    await fireEvent.press(await screen.findByTestId(inboxRowId('SHIP-743')));
    expect(await screen.findByTestId(screenIds.workItem)).toBeOnTheScreen();
  });

  it('shows the ask count as the Inbox tab badge', async () => {
    await openInbox();
    await screen.findByTestId(inboxRowId('SHIP-743'));
    const badge = await screen.findByTestId(tabBadgeId('inbox'));
    expect(badge).toHaveTextContent('15');
  });
});
