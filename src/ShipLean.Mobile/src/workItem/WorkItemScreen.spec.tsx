import { WorkItemActionsProvider } from './WorkItemActions';
import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { QueryClient } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { WorkItemScreen } from '../screens/WorkItemScreen';
import { InboxActionsContext } from '../inbox/InboxActions';
import { InboxSheetsContext } from '../inbox/InboxSheets';
import { links } from '../links';
import { aProject } from '../test-support/builders';
import { apiError } from '../test-support/fakeApi';
import { fakeInboxActions, fakeInboxSheets } from '../test-support/inbox';
import { fakeVoice, renderWithVoice } from '../test-support/voiceUi';
import { aFeatureItem, aStoryItem, workItemApi } from '../test-support/workItem';
import { themes } from '../theme';
import { primitiveIds, uiIds } from '../ui/testIds';
import * as ids from './testIds';

jest.mock('expo-router', () => ({
  ...jest.requireActual('expo-router'),
  useRouter: () => ({ push: mockPush, back: jest.fn(), canGoBack: () => false }),
}));
const mockPush = jest.fn();

const projectId = aProject().id;
const BY_KEY = (key: string) => `GET /api/projects/${projectId}/work-items/by-key/${key}`;

interface Rendered {
  actions: ReturnType<typeof fakeInboxActions>;
  sheets: ReturnType<typeof fakeInboxSheets>;
  queryClient: QueryClient;
}

async function open(
  itemKey: string,
  api: ReturnType<typeof workItemApi>,
  scheme: 'light' | 'dark',
  voice = fakeVoice(),
): Promise<Rendered> {
  api.install();
  const actions = fakeInboxActions();
  const sheets = fakeInboxSheets();
  const ui: ReactElement = (
    <InboxActionsContext.Provider value={actions}>
      <InboxSheetsContext.Provider value={sheets}>
        <WorkItemActionsProvider>
          <WorkItemScreen projectKey="SHIP" itemKey={itemKey} />
        </WorkItemActionsProvider>
      </InboxSheetsContext.Provider>
    </InboxActionsContext.Provider>
  );
  const { queryClient } = await renderWithVoice(ui, { scheme, voice });
  // The asks, states and members reads start with the item: wait for every read to land before the test goes on.
  await waitFor(() => expect(queryClient.isFetching()).toBe(0));
  return { actions, sheets, queryClient };
}

// Test ids in document order, to assert the section order.
function idsInOrder(): string[] {
  const found: string[] = [];
  const walk = (node: unknown): void => {
    if (node === null || typeof node !== 'object') return;
    if (Array.isArray(node)) return node.forEach(walk);
    const n = node as { props?: { testID?: string }; children?: unknown };
    if (typeof n.props?.testID === 'string') found.push(n.props.testID);
    walk(n.children);
  };
  walk(screen.toJSON());
  return found;
}

async function pullDown(): Promise<void> {
  const { refreshControl } = screen.getByTestId(ids.workItemScrollId).props as {
    refreshControl: { props: { onRefresh: () => void } };
  };
  await act(async () => refreshControl.props.onRefresh());
}

describe.each(['light', 'dark'] as const)('Work item screen (%s)', (scheme) => {
  beforeEach(() => mockPush.mockClear());

  // Red when the status line moves below Needs you, or Needs you is not first after it.
  it('renders the feature state: header, status line, Still true, then Needs you first', async () => {
    const feature = aFeatureItem({
      projectId,
      statusVerified: {
        at: '2030-01-01T10:00:00+00:00',
        atLabel: '2h ago',
        stale: false,
        by: null,
        agent: 'Claude Code',
        via: null,
      },
    });
    await open('SHIP-906', workItemApi({}, { projectId, items: [feature] }), scheme);

    expect(await screen.findByTestId(ids.workItemHeaderId)).toBeOnTheScreen();
    expect(screen.getByTestId(ids.workItemTitleId)).toHaveTextContent(feature.title);
    expect(screen.getByTestId(ids.statusTextId)).toHaveTextContent(feature.statusLine);
    expect(screen.getByTestId(ids.statusVerifiedId)).toHaveTextContent('✓ Verified 2h ago');
    expect(screen.getByTestId(ids.statusVerifiedById)).toHaveTextContent('Claude Code (agent)');
    expect(screen.getByTestId(ids.stillTrueId)).toBeOnTheScreen();
    const order = idsInOrder().filter((id) =>
      [
        ids.workItemHeaderId,
        ids.compactProgressId,
        ids.itemActionsRowId,
        ids.statusLineId,
        ids.needsYouSectionId,
        ids.progressSectionId,
        ids.goalDetailsId,
        ids.timelineId,
        primitiveIds.dock,
      ].includes(id),
    );
    expect(order).toEqual([
      ids.workItemHeaderId,
      ids.compactProgressId,
      ids.itemActionsRowId,
      ids.statusLineId,
      ids.needsYouSectionId,
      ids.progressSectionId,
      ids.goalDetailsId,
      ids.timelineId,
      primitiveIds.dock,
    ]);
  });

  // Red when a Story shows the status line or puts Notes before the tasks.
  it('renders the story state: no status line, tasks section before Notes', async () => {
    const story = aStoryItem({ projectId });
    await open('SHIP-921', workItemApi({}, { projectId, items: [story] }), scheme);

    expect(await screen.findByTestId(ids.workItemHeaderId)).toBeOnTheScreen();
    expect(screen.queryByTestId(ids.statusLineId)).toBeNull();
    expect(screen.queryByTestId(ids.stillTrueId)).toBeNull();
    expect(screen.queryByTestId(ids.newStatusId)).toBeNull();
    const order = idsInOrder().filter((id) =>
      [
        ids.workItemHeaderId,
        ids.itemActionsRowId,
        ids.needsYouSectionId,
        ids.storyTasksId,
        ids.acceptanceCriteriaId,
        ids.timelineId,
      ].includes(id),
    );
    expect(order).toEqual([
      ids.workItemHeaderId,
      ids.itemActionsRowId,
      ids.needsYouSectionId,
      ids.storyTasksId,
      ids.acceptanceCriteriaId,
      ids.timelineId,
    ]);
  });

  // Red when the header keeps the state of its first read instead of the cached item.
  it('renders the applied state: the header shows the state from a refetched item', async () => {
    const feature = aFeatureItem({
      projectId,
      state: { id: 's-new', name: 'New', category: 'proposed' },
    });
    const active = { ...feature.state, name: 'Active', category: 'inProgress' as const };
    let reads = 0;
    const api = workItemApi(
      {
        [BY_KEY('SHIP-906')]: (): WorkItemDto => {
          reads += 1;
          return reads === 1 ? feature : { ...feature, state: active };
        },
      },
      { projectId, items: [feature] },
    );
    await open('SHIP-906', api, scheme);
    await screen.findByTestId(ids.workItemHeaderId);
    expect(screen.getByTestId(primitiveIds.statePill)).toHaveTextContent('New');

    await pullDown();

    await waitFor(() =>
      expect(screen.getByTestId(primitiveIds.statePill)).toHaveTextContent('Active'),
    );
  });

  // Red when the screen is hidden or Still true is disabled while the models download, or an "Or say" hint is drawn.
  it('hides the Or say hints in the downloading state', async () => {
    const voice = fakeVoice({
      kind: 'disabled',
      reason: 'downloading',
      permission: 'undetermined',
    });
    const feature = aFeatureItem({ projectId });
    const { actions } = await open(
      'SHIP-906',
      workItemApi({}, { projectId, items: [feature] }),
      scheme,
      voice,
    );

    expect(await screen.findByTestId(ids.statusLineId)).toBeOnTheScreen();
    expect(screen.getByTestId(primitiveIds.dock)).toBeOnTheScreen();
    await fireEvent.press(screen.getByTestId(ids.stillTrueId));
    expect(actions.dispatched.map((d) => d.kind)).toEqual(['stillTrue']);
    expect(screen.queryByText(/^Or say/)).toBeNull();
  });

  // Red when health shows for onTrack, or is hidden for atRisk and blocked.
  it('shows health only when it is at risk or blocked', async () => {
    for (const [health, shown] of [
      ['onTrack', false],
      ['atRisk', true],
      ['blocked', true],
      [null, false],
    ] as const) {
      const feature = aFeatureItem({ projectId, health });
      await open('SHIP-906', workItemApi({}, { projectId, items: [feature] }), scheme);
      expect(await screen.findByTestId(ids.workItemHeaderId)).toBeOnTheScreen();
      expect(screen.queryByTestId(primitiveIds.healthPill) !== null).toBe(shown);
      await screen.unmount();
    }
  });

  // Red when Still true sends revision 0, or the wrong item.
  it('dispatches stillTrue with the item revision when Still true is tapped', async () => {
    const feature = aFeatureItem({ projectId, revision: 7 });
    const { actions } = await open(
      'SHIP-906',
      workItemApi({}, { projectId, items: [feature] }),
      scheme,
    );

    await fireEvent.press(await screen.findByTestId(ids.stillTrueId));

    expect(actions.dispatched).toEqual([
      {
        area: 'inbox',
        projectId,
        itemId: feature.id,
        key: 'SHIP-906',
        kind: 'stillTrue',
        revision: 7,
      },
    ]);
  });

  // Red when the sheet opens in another mode, or without the item's current line.
  it('opens the answer sheet in status mode with the current line', async () => {
    const feature = aFeatureItem({ projectId });
    const { sheets } = await open(
      'SHIP-906',
      workItemApi({}, { projectId, items: [feature] }),
      scheme,
    );

    await fireEvent.press(await screen.findByTestId(ids.newStatusId));

    expect(sheets.calls).toHaveLength(1);
    const call = sheets.calls[0];
    if (call.kind !== 'answer') throw new Error('expected the answer sheet');
    expect(call.request.mode).toBe('status');
    expect(call.request.projectId).toBe(projectId);
    expect(call.request.item.key).toBe('SHIP-906');
    expect(call.request.ask.status?.statusLine).toBe(feature.statusLine);
  });

  // Red when a Feature with no verification shows a Verified chip, or a blank line shows nothing.
  it('shows Not verified yet and No status yet for a Feature never verified', async () => {
    const feature = aFeatureItem({ projectId, statusLine: '', statusVerified: null });
    const { sheets } = await open(
      'SHIP-906',
      workItemApi({}, { projectId, items: [feature] }),
      scheme,
    );

    expect(await screen.findByTestId(ids.statusTextId)).toHaveTextContent('No status yet.');
    expect(screen.getByTestId(ids.statusVerifiedId)).toHaveTextContent('Not verified yet');
    expect(screen.queryByTestId(ids.statusVerifiedById)).toBeNull();
    expect(screen.getByTestId(ids.newStatusId)).toBeOnTheScreen();
    await fireEvent.press(screen.getByTestId(ids.newStatusId));
    expect(sheets.calls).toHaveLength(1);
    const call = sheets.calls[0];
    if (call.kind !== 'answer') throw new Error('expected the answer sheet');
    expect(call.request.mode).toBe('status');
    expect(call.request.ask.status?.statusLine).toBe('');
    expect(call.request.ask.from).toBeNull();
  });

  // Red when the parent tap goes to another key, or a missing owner shows blank.
  it('opens the parent on tap and shows No owner when there is no assignee', async () => {
    const base = aFeatureItem({ projectId });
    const feature = {
      ...base,
      assignee: null,
      parent: { id: 'parent-1', key: 'SHIP-905', type: base.type, title: 'Mobile App' },
    };
    await open('SHIP-906', workItemApi({}, { projectId, items: [feature] }), scheme);

    await fireEvent.press(await screen.findByTestId(ids.workItemParentId));

    expect(mockPush).toHaveBeenCalledWith(links.item('SHIP', 'SHIP-905'));
    expect(screen.getByTestId(ids.workItemOwnerId)).toHaveTextContent('No owner');
  });

  it('shows a stale verification in the warn colour', async () => {
    const feature = aFeatureItem({
      projectId,
      statusVerified: {
        at: '2029-01-01T10:00:00+00:00',
        atLabel: '3 weeks ago',
        stale: true,
        by: null,
        agent: null,
        via: null,
      },
    });
    await open('SHIP-906', workItemApi({}, { projectId, items: [feature] }), scheme);

    const chip = await screen.findByTestId(ids.statusVerifiedId);
    expect(chip).toHaveTextContent('⚠ Verified 3 weeks ago');
    expect(chip).toHaveStyle({ color: themes[scheme].colors.warn });
  });

  // Red when a failed read shows nothing, or Retry does not read again.
  it('shows the error state with Retry when the read fails', async () => {
    const api = workItemApi({}, { projectId });
    api.failNext(BY_KEY('SHIP-906'), { status: 500, body: { code: 'internal_error', error: 'x' } });
    await open('SHIP-906', api, scheme);

    expect(await screen.findByText("Couldn't load SHIP-906.")).toBeOnTheScreen();
    await fireEvent.press(screen.getByTestId(uiIds.errorStateAction));

    expect(await screen.findByTestId(ids.workItemHeaderId)).toBeOnTheScreen();
    expect(screen.queryByTestId(uiIds.errorState)).toBeNull();
  });

  // Red when a 404 shows the error state.
  it('shows the not-found state on a 404', async () => {
    const api = workItemApi({}, { projectId });
    api.failNext(BY_KEY('SHIP-906'), apiError(404, 'not_found', 'No such item.'));
    await open('SHIP-906', api, scheme);

    expect(await screen.findByText("SHIP-906 isn't in this project.")).toBeOnTheScreen();
    expect(screen.queryByTestId(uiIds.errorState)).toBeNull();
  });

  // Red when the pull down does not refetch the item.
  it('refetches on pull down', async () => {
    const api = workItemApi({}, { projectId });
    await open('SHIP-906', api, scheme);
    await screen.findByTestId(ids.workItemHeaderId);
    expect(api.callsTo(BY_KEY('SHIP-906'))).toHaveLength(1);

    await pullDown();

    expect(api.callsTo(BY_KEY('SHIP-906'))).toHaveLength(2);
  });
});
