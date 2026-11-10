import type { InboxItemDto } from '@shiplean/core/api/inbox';
import type { StateDto } from '@shiplean/core/api/projects';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import { UNDO } from '../actions/undo';
import { undoToastId } from '../actions/UndoToastHost.testIds';
import { dockIds } from '../dock/testIds';
import { inboxBtnId, inboxMoreId, inboxRowId } from '../inbox/testIds';
import { screenIds, tabIds } from '../screens/testIds';
import { aConflict, aProject, aWriteResult } from '../test-support/builders';
import { aFeatureStates, FEATURE_STATES } from '../test-support/features';
import type { ApiReply, FakeRequest } from '../test-support/fakeApi';
import { anAnswered } from '../test-support/inbox';
import { designInbox } from '../test-support/inboxDesign';
import { renderApp } from '../test-support/renderApp';
import { kvValues } from '../test-support/storage';
import {
  aDecision,
  aFeatureItem,
  anItemAsks,
  aQuestion,
  NOTE_IDS,
  workItemApi,
} from '../test-support/workItem';
import { primitiveIds } from '../ui/testIds';
import {
  changeStateButtonId,
  needsBtnId,
  stateOptionId,
  workItemHeaderId,
} from '../workItem/testIds';

const PROJECT = aProject().id;
const [proposed, active] = FEATURE_STATES;

// The design Inbox's SHIP-906 (a Feature with D10 and D11 open, and Q1), as the Work item screen reads it. The item's
// id is the Inbox row's, so the row's tap lands on an item the fake knows. The fake PATCH moves the item's state and
// revision, and the by-key read answers with them, so the refetch shows what was sent. `patch` replaces the PATCH.
function build(patch?: () => ApiReply) {
  const inbox = designInbox();
  const row = inbox.items.find((i) => i.key === 'SHIP-906') as InboxItemDto;
  const stateRef = (s: StateDto) => ({ id: s.id, name: s.name, category: s.category });
  const item = aFeatureItem({
    id: row.id,
    projectId: PROJECT,
    revision: 7,
    state: stateRef(proposed!),
  });
  const now = { state: proposed!, revision: item.revision };
  const decisions = [10, 11].map((n) => aDecision({ number: n, label: `D${n}` }));
  const asks = anItemAsks(item.id, { decisions, questions: [aQuestion()] });
  const api = workItemApi(
    {
      [`GET /api/projects/${PROJECT}/inbox`]: inbox,
      [`GET /api/projects/${PROJECT}/inbox/answered`]: anAnswered(),
      [`GET /api/projects/${PROJECT}/states`]: aFeatureStates(),
      [`GET /api/projects/${PROJECT}/work-items/by-key/${item.key}`]: () => ({
        ...item,
        revision: now.revision,
        state: stateRef(now.state),
      }),
      [`PATCH /api/work-items/${item.id}`]:
        patch ??
        ((request: FakeRequest) => {
          const body = request.body as { changes: { state: { value: string } } };
          now.state = FEATURE_STATES.find((s) => s.id === body.changes.state.value) ?? now.state;
          now.revision += 1;
          return aWriteResult({ id: item.id, key: item.key, revision: now.revision });
        }),
    },
    { items: [item], asks: [asks], projectId: PROJECT },
  );
  return { api, item, now };
}

const ROUTES = (item: WorkItemDto) => ({
  confirm: `POST /api/work-items/${item.id}/decisions/confirm`,
  comments: `POST /api/work-items/${item.id}/comments`,
  patch: `PATCH /api/work-items/${item.id}`,
  read: `GET /api/projects/${PROJECT}/work-items/by-key/${item.key}`,
});

const advance = (ms: number) => act(async () => jest.advanceTimersByTimeAsync(ms));

// Projects, then SHIP, then the Inbox tab, then SHIP-906's row: the Work item screen.
async function openFeatureFromInbox(api: ReturnType<typeof workItemApi>) {
  await renderApp({ url: '/', api });
  await fireEvent.press(await screen.findByTestId('project-row-SHIP'));
  await fireEvent.press(await screen.findByTestId(tabIds.inbox));
  await fireEvent.press(await screen.findByTestId(inboxRowId('SHIP-906')));
  expect(await screen.findByTestId(screenIds.workItem)).toBeOnTheScreen();
  expect(await screen.findByTestId(workItemHeaderId)).toBeOnTheScreen();
}

async function sendNote(text: string) {
  await fireEvent.press(await screen.findByTestId(dockIds.open));
  await fireEvent.press(screen.getByTestId(dockIds.modeNote));
  await fireEvent.changeText(screen.getByTestId(dockIds.noteField), text);
  await fireEvent.press(screen.getByTestId(dockIds.post));
}

describe('Inbox to Work item journey', () => {
  beforeEach(() => {
    // The runner keeps its outbox in the key-value store: start each journey empty.
    kvValues.clear();
    jest.useFakeTimers({ doNotFake: ['nextTick', 'queueMicrotask', 'setImmediate'] });
  });
  afterEach(() => jest.useRealTimers());

  it('opens a Feature from the Inbox, approves D10, adds a note and sets it Active', async () => {
    const { api, item, now } = build();
    const route = ROUTES(item);
    await openFeatureFromInbox(api);
    expect(api.callsTo(route.read).length).toBeGreaterThan(0);
    expect(screen.getByTestId('wi-needs-you')).toBeOnTheScreen();

    // 2. Approve D10: held 5 s, then one confirm.
    await fireEvent.press(screen.getByTestId(needsBtnId('D10', 'approve')));
    await advance(UNDO.tap.ms - 1);
    expect(api.callsTo(route.confirm)).toHaveLength(0);
    await advance(1);
    await waitFor(() => expect(api.callsTo(route.confirm)).toHaveLength(1));
    expect(api.callsTo(route.confirm)[0]!.body).toEqual({ numbers: [10] });

    // 3. A typed note goes at once.
    await sendNote('Sam says the fix lands tomorrow.');
    await waitFor(() => expect(api.callsTo(route.comments)).toHaveLength(1));
    expect(api.callsTo(route.comments)[0]!.body).toMatchObject({
      body: 'Sam says the fix lands tomorrow.',
    });

    // 4. Active: held 5 s, then one PATCH at the item's revision.
    await fireEvent.press(screen.getByTestId(changeStateButtonId));
    await fireEvent.press(await screen.findByTestId(stateOptionId(active!.name)));
    await advance(UNDO.tap.ms - 1);
    expect(api.callsTo(route.patch)).toHaveLength(0);
    await advance(1);
    await waitFor(() => expect(api.callsTo(route.patch)).toHaveLength(1));
    expect(api.callsTo(route.patch)[0]!.body).toMatchObject({
      expectedRevision: 7,
      changes: { state: { value: active!.id } },
    });
    expect(now.state).toBe(active);
  });

  it('undoes a typed note by archiving it', async () => {
    const { api, item } = build();
    const route = ROUTES(item);
    await openFeatureFromInbox(api);
    await sendNote('A note to take back.');
    await waitFor(() => expect(api.callsTo(route.comments)).toHaveLength(1));
    await fireEvent.press(await screen.findByTestId(primitiveIds.toastAction));
    const archive = `POST /api/comments/${NOTE_IDS[0]}/archive`;
    await waitFor(() => expect(api.callsTo(archive)).toHaveLength(1));
  });

  it('sends nothing for an approval undone inside its window', async () => {
    const { api, item } = build();
    await renderApp({ url: '/p/SHIP/inbox', api });
    await fireEvent.press(await screen.findByTestId(inboxMoreId('SHIP-906')));
    await fireEvent.press(await screen.findByTestId(inboxBtnId('SHIP-906', 'D10', 'approve')));
    expect(await screen.findByTestId(undoToastId)).toBeOnTheScreen();
    await advance(2000);
    await fireEvent.press(screen.getByTestId(primitiveIds.toastAction));
    await advance(10_000);
    expect(api.callsTo(ROUTES(item).confirm)).toHaveLength(0);
  });

  const stateChangeRefused = async () => {
    const built = build(() => ({
      status: 409,
      body: aConflict(aFeatureItem({ projectId: PROJECT, revision: 9 })),
    }));
    const route = ROUTES(built.item);
    await openFeatureFromInbox(built.api);
    const reads = built.api.callsTo(route.read).length;
    await fireEvent.press(screen.getByTestId(changeStateButtonId));
    await fireEvent.press(await screen.findByTestId(stateOptionId(active!.name)));
    await advance(UNDO.tap.ms);
    await waitFor(() => expect(built.api.callsTo(route.patch)).toHaveLength(1));
    return { ...built, route, reads };
  };

  // Red on purpose (SHIP-1195): WorkItemScreen reads the item again only on pull down (Loaded.refresh,
  // screens/WorkItemScreen.tsx:111-124); nothing refreshes it when a setState is sent, so the header keeps the old state.
  it('SHIP-1195 shows Active in the header after the state change is sent', async () => {
    const { api, item } = build();
    await openFeatureFromInbox(api);
    await fireEvent.press(screen.getByTestId(changeStateButtonId));
    await fireEvent.press(await screen.findByTestId(stateOptionId(active!.name)));
    await advance(UNDO.tap.ms);
    await waitFor(() => expect(api.callsTo(ROUTES(item).patch)).toHaveLength(1));
    await waitFor(() =>
      expect(
        within(screen.getByTestId(workItemHeaderId)).getByTestId(primitiveIds.statePill),
      ).toHaveTextContent(active!.name),
    );
  });

  // Red on purpose (SHIP-1195): the same missing refresh; a refused send leaves the screen as it was.
  it('SHIP-1195 re-reads the screen after a 409 on a state change', async () => {
    const { api, route, reads } = await stateChangeRefused();
    await waitFor(() => expect(api.callsTo(route.read).length).toBeGreaterThan(reads));
  });

  // Red on purpose (SHIP-1195): nothing on the Work item screen shows a failed setState. The runner's pending entry
  // carries "Changed on the server. Open SHIP-906 to check.", but only the Needs you cards, the Inbox rows and the
  // voice banner read a failed entry.
  it('SHIP-1195 shows the conflict copy after a 409 on a state change', async () => {
    await stateChangeRefused();
    expect(await screen.findByText(/Changed on the server/)).toBeOnTheScreen();
  });
});
