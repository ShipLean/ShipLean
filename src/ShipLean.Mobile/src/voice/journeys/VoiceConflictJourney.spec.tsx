// A voice 409 on the Features list, the Inbox and the Backlog, through the real app with a stateful fake
// server: someone else edits the item between the preview and Apply, the server refuses the stale revision, the screen
// reads its data again, and the preview comes back with the changed row marked; Apply then sends the new revision.
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import type { BulkWriteResult } from '@shiplean/core/api/backlogWrites';
import type { FeaturesOverviewDto } from '@shiplean/core/api/featuresOverview';
import type { InboxDto } from '@shiplean/core/api/inbox';
import { backlogRowId } from '../../backlog/testIds';
import { featureRowId } from '../../features/testIds';
import { inboxRowId } from '../../inbox/testIds';
import { readyCoachMarkIds } from '../../setup/ReadyCoachMark.testIds';
import { aBacklogView, backlogApi, designFeatures } from '../../test-support/backlog';
import { aConflict, aProject, aWorkItem, aWriteResult } from '../../test-support/builders';
import { controlledApi } from '../../test-support/controlledApi';
import type { FakeApi, FakeRequest } from '../../test-support/fakeApi';
import { FEATURE_STATES, designOverview, featuresApi } from '../../test-support/features';
import { inboxApi } from '../../test-support/inbox';
import { designInbox } from '../../test-support/inboxDesign';
import { setConnected } from '../../test-support/netInfo';
import { renderApp } from '../../test-support/renderApp';
import { signedInApi } from '../../test-support/signedInApi';
import { kvValues } from '../../test-support/storage';
import { installFakeEngines } from '../../test-support/voiceUi';
import type { ParsedCall } from '../contracts';
import { micSlotId, type MicPlacement } from '../MicSlot.testIds';
import { applyFailedSheetId } from '../sheets/ApplyFailedSheet.testIds';
import { previewApplyId, previewMarkId, previewSheetId } from '../sheets/PreviewSheet.testIds';

const NOW = new Date('2030-01-01T09:00:00.000Z');
const PROJECT = aProject().id;

// Each fixture is built once: its ids are random, and callsTo needs the real ones.
const OVERVIEW = designOverview();
const INBOX = designInbox(PROJECT);
const FEATURES = designFeatures();

const ACTIVE = FEATURE_STATES.find((s) => s.name === 'Active')!;
const FEATURE = OVERVIEW.groups.flatMap((g) => g.rows).find((r) => r.key === 'SHIP-892')!;
const STATUS_ITEM = INBOX.items.find((i) => i.key === 'SHIP-743')!;
const MOVED = FEATURES.find((r) => r.key === 'SHIP-906')!;
const ABOVE = FEATURES.find((r) => r.key === 'SHIP-907')!;

const conflict = (id: string, key: string, revision: number) => ({
  status: 409,
  body: aConflict(aWorkItem({ id, key, revision })),
});

// The Features server: SHIP-892's revision is kept, the overview shows it, a PATCH at it moves it on and a stale one is
// a 409.
function featuresServer() {
  let revision = FEATURE.revision;
  const overview = (): FeaturesOverviewDto => ({
    ...OVERVIEW,
    groups: OVERVIEW.groups.map((g) => ({
      ...g,
      rows: g.rows.map((r) => (r.key === FEATURE.key ? { ...r, revision } : r)),
    })),
  });
  const route = `PATCH /api/work-items/${FEATURE.id}`;
  const api = featuresApi(
    {
      [`GET /api/projects/${PROJECT}/features/overview`]: () => overview(),
      [route]: (request: FakeRequest) => {
        const body = request.body as { expectedRevision: number };
        if (body.expectedRevision !== revision) return conflict(FEATURE.id, FEATURE.key, revision);
        revision += 1;
        return aWriteResult({ id: FEATURE.id, key: FEATURE.key, revision });
      },
    },
    { projectId: PROJECT, overview: OVERVIEW },
  );
  const sent = () =>
    api
      .callsTo(route)
      .map((c) => (c.body as { expectedRevision: number; changes: unknown }).expectedRevision);
  return {
    api,
    sent,
    // Someone else edits SHIP-892 on the web: the revision moves on.
    editElsewhere: () => {
      revision += 1;
    },
  };
}

// The Inbox server: SHIP-743's revision is kept, the Inbox shows it, a status confirm at it moves it on and a stale one
// is a 409.
function inboxServer() {
  let revision = STATUS_ITEM.revision;
  const inbox = (): InboxDto => ({
    ...INBOX,
    items: INBOX.items.map((i) => (i.key === STATUS_ITEM.key ? { ...i, revision } : i)),
  });
  const route = `POST /api/work-items/${STATUS_ITEM.id}/status/confirm`;
  const api = inboxApi({
    [`GET /api/projects/${PROJECT}/inbox`]: () => inbox(),
    [route]: (request: FakeRequest) => {
      const body = request.body as { revision: number };
      if (body.revision !== revision) return conflict(STATUS_ITEM.id, STATUS_ITEM.key, revision);
      revision += 1;
      return aWorkItem({ id: STATUS_ITEM.id, key: STATUS_ITEM.key, revision });
    },
  });
  const sent = () => api.callsTo(route).map((c) => (c.body as { revision: number }).revision);
  return {
    api,
    sent,
    editElsewhere: () => {
      revision += 1;
    },
  };
}

// The Backlog server: SHIP-906's revision is kept, the Features level shows it, a bulk placement at it moves it on and a
// stale one comes back in stale (the client turns that into a 409).
function backlogServer() {
  let revision = MOVED.revision;
  const rows = () => FEATURES.map((r) => (r.key === MOVED.key ? { ...r, revision } : r));
  const route = `POST /api/projects/${PROJECT}/backlog/bulk`;
  const api = controlledApi({
    ...signedInApi(),
    ...backlogApi(
      {
        [`GET /api/projects/${PROJECT}/backlogs/features`]: () => aBacklogView('features', rows()),
        [route]: (request: FakeRequest): BulkWriteResult => {
          const [item] = (request.body as { items: { id: string; expectedRevision: number }[] })
            .items;
          if (item.expectedRevision !== revision)
            return {
              written: [],
              stale: [conflict(MOVED.id, MOVED.key, revision).body],
            };
          revision += 1;
          return { written: [aWriteResult({ id: MOVED.id, key: MOVED.key, revision })], stale: [] };
        },
      },
      { projectId: PROJECT, rows: { features: FEATURES } },
    ),
    [`GET /api/projects/${PROJECT}/members`]: [],
  });
  const sent = () =>
    api
      .callsTo(route)
      .map((c) => (c.body as { items: { expectedRevision: number }[] }).items[0].expectedRevision);
  return {
    api,
    sent,
    editElsewhere: () => {
      revision += 1;
    },
  };
}

interface ConflictRun {
  name: string;
  url: string;
  server: () => { api: FakeApi; sent: () => number[]; editElsewhere: () => void };
  loaded: string;
  mic: MicPlacement;
  calls: ParsedCall[];
  // The preview line of the refused change.
  line: string;
  revision: number;
}

const RUNS: ConflictRun[] = [
  {
    name: 'Features',
    url: '/p/SHIP/features',
    server: featuresServer,
    loaded: featureRowId(FEATURE.key),
    mic: 'tabBar',
    calls: [{ action: 'set_state', args: { feature: FEATURE.key, state: ACTIVE.name } }],
    line: `features:${FEATURE.id}:setState`,
    revision: FEATURE.revision,
  },
  {
    name: 'Inbox',
    url: '/p/SHIP/inbox',
    server: inboxServer,
    loaded: inboxRowId(STATUS_ITEM.key),
    mic: 'tabBar',
    calls: [{ action: 'stillTrue', args: { key: STATUS_ITEM.key } }],
    line: `inbox:${STATUS_ITEM.id}:stillTrue:Status`,
    revision: STATUS_ITEM.revision,
  },
  {
    name: 'Backlog',
    url: '/p/SHIP/backlog',
    server: backlogServer,
    loaded: backlogRowId(MOVED.key),
    mic: 'tabBar',
    calls: [
      {
        action: 'move_relative',
        args: { item: MOVED.key, relation: 'above', other: ABOVE.key },
      },
    ],
    line: `move:${MOVED.id}`,
    revision: MOVED.revision,
  },
];

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
  setConnected(true);
});
afterEach(async () => {
  await act(async () => {
    await jest.runOnlyPendingTimersAsync();
  });
  jest.useRealTimers();
});

describe('VoiceConflictJourney', () => {
  it.each(RUNS.map((r) => [r.name, r] as const))(
    'on the %s, a 409 brings the preview back with the changed row marked, and Apply sends the new revision',
    async (_name, run) => {
      const { api, sent, editElsewhere } = run.server();
      const { model, recognizer } = installFakeEngines();
      await renderApp({ url: run.url, api });
      await screen.findByTestId(run.loaded);
      // Voice is ready, so the "Try it" coach mark shows at the mic; Skip puts it away for good.
      await fireEvent.press(await screen.findByTestId(readyCoachMarkIds.skip));

      recognizer.script([], 'the change');
      model.respond(run.calls);
      await hold(run.mic);
      const first = await screen.findByTestId(previewSheetId);
      editElsewhere();
      await fireEvent.press(within(first).getByTestId(previewApplyId));

      await waitFor(() => expect(sent()).toEqual([run.revision]));
      const mark = await screen.findByTestId(previewMarkId(run.line));
      const preview = screen.getByTestId(previewSheetId);
      expect(within(preview).getByTestId(previewMarkId(run.line))).toBe(mark);
      expect(screen.queryByTestId(applyFailedSheetId)).toBeNull();

      await fireEvent.press(within(preview).getByTestId(previewApplyId));
      // Sent again at the revision the other edit left, so it lands.
      await waitFor(() => expect(sent()).toEqual([run.revision, run.revision + 1]));
      expect(screen.queryByTestId(applyFailedSheetId)).toBeNull();
    },
  );
});
