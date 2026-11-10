import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import type { Pending } from '../../actions/types';
import { FeaturesActionsProvider } from '../../features/FeaturesActions';
import { FeatureDetails } from '../../features/details/FeatureDetails';
import { InboxActionsContext } from '../../inbox/InboxActions';
import { InboxSheetsContext } from '../../inbox/InboxSheets';
import { inboxRowId } from '../../inbox/testIds';
import { screenIds } from '../../screens/testIds';
import { fromSetupTipIds } from '../../setup/FromSetupTip.testIds';
import { readyCoachMarkIds } from '../../setup/ReadyCoachMark.testIds';
import { aPending, fakeRunner } from '../../test-support/actions';
import { TEST_ORIGIN, aMe, aProject } from '../../test-support/builders';
import { controlledApi } from '../../test-support/controlledApi';
import { backlogApi } from '../../test-support/backlog';
import { aFeatureGroup, aFeatureRow, anOverview, featuresApi } from '../../test-support/features';
import { fakeInboxActions, fakeInboxSheets, inboxApi } from '../../test-support/inbox';
import { designInbox } from '../../test-support/inboxDesign';
import { renderApp } from '../../test-support/renderApp';
import { renderWithRunner } from '../../test-support/renderWithRunner';
import { retroApi } from '../../test-support/retro';
import { signedInApi } from '../../test-support/signedInApi';
import { kvValues } from '../../test-support/storage';
import { FakeModelStore } from '../../test-support/voice';
import { installFakeEngines } from '../../test-support/voiceUi';
import { aFeatureItem, aHistory, workItemApi } from '../../test-support/workItem';
import { primitiveIds } from '../../ui/testIds';
import type { WorkItemAction } from '../../workItem/actions';
import { workItemHeaderId } from '../../workItem/testIds';
import { voiceChangeId, voiceChangeRowId } from '../after/VoiceChangeRow.testIds';
import { voiceSendFailedRefusedId } from '../after/VoiceSendFailedBanner.testIds';
import { recordFailure, resetSilentHolds, silentHolds } from '../failures/silentHolds';
import { micSlotId, type MicPlacement } from '../MicSlot.testIds';

const NOW = new Date('2030-01-01T09:00:00.000Z');
const PROJECT = aProject().id;
const OUTBOX = `shiplean.outbox\n${TEST_ORIGIN}\n${aMe().email}`;
const ITEM = aFeatureItem({ projectId: PROJECT });
const ITEM_URL = `/p/SHIP/items/${ITEM.key}`;

// A voice change to the Work item's Feature, made a minute ago.
const SET_HEALTH: WorkItemAction = {
  area: 'workItem',
  projectId: PROJECT,
  itemId: ITEM.id,
  key: ITEM.key,
  kind: 'setHealth',
  revision: ITEM.revision,
  health: 'blocked',
  from: null,
};
const voicePending = (overrides: Partial<Pending>): Pending =>
  aPending({
    action: SET_HEALTH,
    origin: 'voice',
    createdAt: new Date(NOW.getTime() - 60_000).toISOString(),
    ...overrides,
  });
const sent = voicePending({ id: 'p-sent', batchId: 'b-sent', state: 'sent' });
const refused = voicePending({
  id: 'p-refused',
  batchId: 'b-refused',
  state: 'failed',
  failure: 'validation',
  error: 'That health is not allowed.',
});

// The per-session runner loads this outbox when the app opens, as after a restart.
function seedOutbox(...items: Pending[]) {
  kvValues.set(
    OUTBOX,
    JSON.stringify({
      items,
      batches: Object.fromEntries(
        items.map((p) => [p.batchId, { mode: 'after', allOrNothing: false, live: false }]),
      ),
      sentAt: Object.fromEntries(
        items.filter((p) => p.state === 'sent').map((p) => [p.id, p.createdAt]),
      ),
    }),
  );
}

const inbox = () => inboxApi({}, { inbox: designInbox() });
const workItem = () =>
  workItemApi(
    { [`GET /api/projects/${PROJECT}/inbox`]: designInbox() },
    { projectId: PROJECT, items: [ITEM], history: aHistory({ revisions: [], asks: [] }) },
  );

// Each route with an API that loads it, and what shows once its spec is registered.
const ROUTES = [
  { url: '/p/SHIP/inbox', screen: 'inbox', api: inbox, loaded: inboxRowId('SHIP-743') },
  {
    url: '/p/SHIP/backlog',
    screen: 'backlog',
    api: () =>
      controlledApi({
        ...signedInApi(),
        ...backlogApi({}, { projectId: PROJECT }),
        [`GET /api/projects/${PROJECT}/members`]: [],
      }),
    loaded: screenIds.backlog,
  },
  {
    url: '/p/SHIP/features',
    screen: 'features',
    api: () => featuresApi({}, { projectId: PROJECT }),
    loaded: screenIds.features,
  },
  {
    url: '/p/SHIP/retro',
    screen: 'retro',
    api: () => retroApi(signedInApi(), { projectId: PROJECT }),
    loaded: screenIds.retro,
  },
] as const;

const at = { nativeEvent: { locationX: 20, locationY: 20 } };
// Press the mic, hold it, let go, and let the parse settle.
async function hold(placement: MicPlacement) {
  const mic = screen.getAllByTestId(micSlotId(placement))[0];
  await act(async () => fireEvent(mic, 'responderGrant', at));
  await act(async () => {
    jest.advanceTimersByTime(900);
  });
  await act(async () => fireEvent(mic, 'responderRelease', at));
  await act(async () => {
    jest.advanceTimersByTime(0);
  });
}

beforeEach(() => {
  jest.useFakeTimers({ now: NOW });
  for (const k of [...kvValues.keys()])
    if (k.startsWith('shiplean.outbox') || k.startsWith('shiplean.voice.')) kvValues.delete(k);
  resetSilentHolds();
});
afterEach(() => jest.useRealTimers());

describe('Wiring', () => {
  it('shows Your voice change on the Work item after a voice batch is sent', async () => {
    seedOutbox(sent);
    await renderApp({ url: ITEM_URL, api: workItem() });

    const row = await screen.findByTestId(voiceChangeRowId);
    expect(await within(row).findByTestId(voiceChangeId('b-sent'))).toBeOnTheScreen();
    expect(within(screen.getByTestId(screenIds.workItem)).getByTestId(voiceChangeRowId)).toBe(row);
  });

  it('shows Your voice change in a Features expanded row', async () => {
    const row = aFeatureRow({ id: ITEM.id, key: ITEM.key });
    featuresApi(
      {
        'GET /api/me': aMe(),
        [`GET /api/work-items/${ITEM.id}/history`]: aHistory({ revisions: [], asks: [] }),
      },
      {
        projectId: PROJECT,
        overview: anOverview([aFeatureGroup('inProgress', [row])], { projectId: PROJECT }),
      },
    );
    const runner = fakeRunner();
    runner.setPending([sent]);
    await renderWithRunner(
      <InboxActionsContext.Provider value={fakeInboxActions()}>
        <InboxSheetsContext.Provider value={fakeInboxSheets()}>
          <FeaturesActionsProvider>
            <FeatureDetails row={row} projectId={PROJECT} projectKey="SHIP" />
          </FeaturesActionsProvider>
        </InboxSheetsContext.Provider>
      </InboxActionsContext.Provider>,
      { runner },
    );

    const changes = await screen.findByTestId(voiceChangeRowId);
    expect(await within(changes).findByTestId(voiceChangeId('b-sent'))).toBeOnTheScreen();
  });

  it('shows the send-failed banner at each tab root for a refused queued voice change', async () => {
    for (const route of [...ROUTES, { url: ITEM_URL, api: workItem }]) {
      seedOutbox(refused);
      const app = await renderApp({ url: route.url, api: route.api() });
      expect(await screen.findByTestId(voiceSendFailedRefusedId('p-refused'))).toBeOnTheScreen();
      await app.unmount();
    }
  });

  it('shows the from-setup tip on the Inbox opened from setup, once', async () => {
    const first = await renderApp({ url: '/p/SHIP/inbox?from=setup', api: inbox() });
    const tip = await screen.findByTestId(fromSetupTipIds.card);
    expect(within(screen.getByTestId(screenIds.inbox)).getByTestId(fromSetupTipIds.card)).toBe(tip);
    await first.unmount();

    await renderApp({ url: '/p/SHIP/inbox?from=setup', api: inbox() });
    await screen.findByTestId(inboxRowId('SHIP-743'));
    expect(screen.getByTestId(fromSetupTipIds.root)).toBeOnTheScreen();
    expect(screen.queryByTestId(fromSetupTipIds.card)).toBeNull();
  });

  it("shows the ready coach mark at the tab bar's mic when voice first becomes ready", async () => {
    const models = new FakeModelStore();
    models.set({ phase: 'none', resolved: null });
    installFakeEngines({ models });
    await renderApp({ url: '/p/SHIP/inbox', api: inbox() });
    await screen.findByTestId(primitiveIds.tabBar);
    expect(screen.getByTestId(readyCoachMarkIds.root)).toBeOnTheScreen();
    expect(screen.queryByTestId(readyCoachMarkIds.card)).toBeNull();

    await act(async () => models.set({ phase: 'ready', resolved: 'qwen3-1.7b' }));

    expect(await screen.findByTestId(readyCoachMarkIds.card)).toBeOnTheScreen();
    // One tap puts it away for good.
    await fireEvent.press(screen.getByTestId(readyCoachMarkIds.skip));
    expect(screen.queryByTestId(readyCoachMarkIds.card)).toBeNull();
  });

  it('registers a voice spec on every tab root and the Work item', async () => {
    const routes = [
      ...ROUTES.map((r) => ({ ...r, mic: 'tabBar' as const })),
      { url: ITEM_URL, screen: 'workItem', api: workItem, loaded: workItemHeaderId, mic: 'dock' },
    ] as const;
    for (const route of routes) {
      const { model, recognizer } = installFakeEngines();
      recognizer.script([], 'what is this');
      const app = await renderApp({ url: route.url, api: route.api() });
      await screen.findByTestId(route.loaded);
      await hold(route.mic);
      await waitFor(() => expect([route.url, model.prepared]).toEqual([route.url, [route.screen]]));
      await app.unmount();
    }
  });

  it('starts the silent-hold count again when a hold is answered', async () => {
    const { model, recognizer } = installFakeEngines();
    await renderApp({ url: '/p/SHIP/inbox', api: inbox() });
    await screen.findByTestId(inboxRowId('SHIP-743'));
    recordFailure({ kind: 'nothingHeard', detail: {} });
    recordFailure({ kind: 'nothingHeard', detail: {} });
    expect(silentHolds()).toBe(2);

    recognizer.script([], 'what needs me');
    model.respond([{ action: 'whatNeedsMe', args: {} }]);
    await hold('tabBar');

    await waitFor(() => expect(model.parsed).toHaveLength(1));
    expect(silentHolds()).toBe(0);
  });
});
