import type { AnsweredDto, InboxDto } from '@shiplean/core/api/inbox';
import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { outboxKey } from '../actions/outbox';
import { scopeOf } from '../actions/scope';
import type { Pending } from '../actions/types';
import { TEST_ORIGIN, aMe, aProject } from '../test-support/builders';
import type { ApiHandler } from '../test-support/fakeApi';
import { anAnswered, anAnsweredRow, inboxApi } from '../test-support/inbox';
import { designInbox } from '../test-support/inboxDesign';
import { signedInApi } from '../test-support/signedInApi';
import { setConnected } from '../test-support/netInfo';
import { renderApp } from '../test-support/renderApp';
import { kvValues } from '../test-support/storage';
import { accountIds } from '../screens/account/testIds';
import { projectsIds } from '../screens/projects/testIds';
import { screenIds, tabIds } from '../screens/testIds';
import { optionId } from '../ui/testIds';
import {
  answeredNextId,
  answeredPendingStatusId,
  answeredRowId,
  inboxBtnId,
  inboxIds,
  inboxMoreId,
  inboxRowId,
} from './testIds';

const project = aProject();
const inbox = designInbox(project.id);
const ITEM = 'SHIP-906';
const item = inbox.items.find((i) => i.key === ITEM)!;
const CONFIRM = `POST /api/work-items/${item.id}/decisions/confirm`;
const INBOX_ROUTE = `GET /api/projects/${project.id}/inbox`;
const ANSWERED_ROUTE = `GET /api/projects/${project.id}/inbox/answered`;
const URL = '/p/SHIP/inbox';

// The Inbox once D10 is answered on the server: the ask is closed, the item's other asks stay.
const withoutD10 = (): InboxDto => ({
  ...inbox,
  openAsks: inbox.openAsks - 1,
  items: inbox.items.map((i) =>
    i.id === item.id ? { ...i, asks: i.asks.filter((a) => a.label !== 'D10') } : i,
  ),
});

const pickedUp = (): AnsweredDto =>
  anAnswered({
    rows: [
      anAnsweredRow({
        itemId: item.id,
        key: ITEM,
        kind: 'decision',
        labels: 'D10',
        summary: 'Approved D10',
        next: {
          kind: 'agentPickedUp',
          by: null,
          agentName: 'Claude Code',
          at: '2026-11-04T10:30:00+00:00',
          atLabel: '10:30',
        },
      }),
    ],
  });

beforeEach(() => {
  // The phone's outbox and kept Inbox live in the kv store, which the jest setup doesn't reset between specs.
  kvValues.clear();
  down = false;
  jest.useFakeTimers();
});

afterEach(async () => {
  await act(async () => setConnected(true));
  jest.useRealTimers();
});

const tap = (testID: string) => fireEvent.press(screen.getByTestId(testID));
const passSeconds = (s: number) => act(() => jest.advanceTimersByTimeAsync(s * 1000));
// The phone's connection and the server's reach move together: while down, the Inbox's own routes and the confirm
// route get no response, as fetch does with no connection. The projects list and the rest of the signed-in reads fail
// too: a cold start offline opens the project from the list kept on the phone, never from the network.
let down = false;
const unlessDown =
  <T,>(answer: () => T): ApiHandler =>
  () => {
    if (down) throw new TypeError('Network request failed');
    return answer() as ReturnType<ApiHandler>;
  };
const goOffline = () => {
  down = true;
  return act(async () => setConnected(false));
};
const goOnline = () => {
  down = false;
  return act(async () => setConnected(true));
};
// What the outbox holds for the signed-in test user, as the runner wrote it to the kv store.
const pendingStatus = () => screen.getByTestId(answeredPendingStatusId(outboxItems()[0]!.id));
const outboxItems = (): Pending[] => {
  const scope = scopeOf({ origin: TEST_ORIGIN, email: aMe().email }) as string;
  const raw = kvValues.get(outboxKey(scope));
  return raw === undefined ? [] : (JSON.parse(raw) as { items: Pending[] }).items;
};
// Every signed-in read (me, projects, health) goes down with the connection.
const signedInUnlessDown = (): Record<string, ApiHandler> =>
  Object.fromEntries(
    Object.entries(signedInApi()).map(([route, body]) => [
      route,
      unlessDown(() => body) as ApiHandler,
    ]),
  );

async function openInbox(api: ReturnType<typeof inboxApi>) {
  const app = await renderApp({ url: URL, api });
  await screen.findByTestId(inboxRowId(ITEM));
  return app;
}

async function approveD10() {
  await tap(inboxMoreId(ITEM));
  await tap(inboxBtnId(ITEM, 'D10', 'approve'));
  await passSeconds(5);
}

describe('the Inbox offline', () => {
  it('keeps an answer given offline across a restart and sends it once online', async () => {
    let confirmed = false;
    const api = inboxApi(
      {
        ...signedInUnlessDown(),
        [CONFIRM]: unlessDown(() => {
          confirmed = true;
          return { status: 204 };
        }),
        [INBOX_ROUTE]: unlessDown(() => (confirmed ? withoutD10() : inbox)),
        [ANSWERED_ROUTE]: unlessDown(() => (confirmed ? pickedUp() : anAnswered({ rows: [] }))),
      },
      { inbox },
    );
    const first = await openInbox(api);

    // 1. Offline, approve D10: Answered says it waits, and the banner shows.
    await goOffline();
    await approveD10();
    await tap(optionId(inboxIds.segment, 'answered'));
    expect(pendingStatus()).toHaveTextContent('Sends when you’re online');
    expect(screen.getByTestId(inboxIds.offlineBanner)).toBeOnTheScreen();
    expect(api.callsTo(CONFIRM)).toHaveLength(0);
    const queuedId = answeredPendingStatusId(outboxItems()[0]!.id);

    // 2. Restart over the same kv store with no connection at all: the project opens from the kept projects list, and
    // the kept Inbox shows with the offline banner and the queued answer. renderApp empties the query cache, so the row
    // can only be the one kept on the phone, and the answer only the one in the outbox.
    await first.unmount();
    await renderApp({ url: URL, api });
    expect(await screen.findByTestId(inboxRowId(ITEM))).toBeOnTheScreen();
    expect(screen.getByTestId(inboxIds.offlineBanner)).toBeOnTheScreen();
    await tap(optionId(inboxIds.segment, 'answered'));
    expect(screen.getByTestId(queuedId)).toHaveTextContent('Sends when you’re online');
    expect(api.callsTo(CONFIRM)).toHaveLength(0);

    // Ten seconds offline later, still nothing sent.
    await passSeconds(10);
    expect(api.callsTo(CONFIRM)).toHaveLength(0);

    // The connection comes back: the answer goes out once, and the restart queued no second.
    await goOnline();
    await passSeconds(5);
    expect(api.callsTo(CONFIRM)).toHaveLength(1);

    // 3. The send answered: it went out exactly once.
    expect(api.callsTo(CONFIRM)[0]!.body).toEqual({ numbers: [10] });

    // 4. Answered refetches: the server's row says who picked it up, and the queued line is gone.
    expect(await screen.findByTestId(answeredRowId(ITEM))).toBeOnTheScreen();
    expect(screen.getByTestId(answeredNextId(ITEM))).toHaveTextContent(
      'Claude Code picked it up · 10:30',
    );
    expect(screen.queryByTestId(queuedId)).toBeNull();
    expect(api.callsTo(CONFIRM)).toHaveLength(1);
  });

  it('does not post twice when a send gets no response', async () => {
    let confirmed = false;
    let first = true;
    const api = inboxApi(
      {
        // The server takes the answer, and the phone never hears back.
        [CONFIRM]: () => {
          confirmed = true;
          if (!first) return { status: 204 };
          first = false;
          throw new TypeError('Network request failed');
        },
        [INBOX_ROUTE]: () => (confirmed ? withoutD10() : inbox),
        [ANSWERED_ROUTE]: () => (confirmed ? pickedUp() : anAnswered({ rows: [] })),
      },
      { inbox },
    );
    const app = await openInbox(api);

    await approveD10();
    await passSeconds(5);

    // It is uncertain: sent once, no response, nothing resent.
    expect(api.callsTo(CONFIRM)).toHaveLength(1);
    await tap(optionId(inboxIds.segment, 'answered'));
    const queuedId = answeredPendingStatusId(outboxItems()[0]!.id);
    expect(pendingStatus()).toHaveTextContent('Sending…');
    // 'Sending…' is shared by sending and uncertain: the outbox says which.
    expect(outboxItems().map((p) => p.state)).toEqual(['uncertain']);

    // The connection returns: reconcile reads a fresh Inbox, sees D10 closed and settles it as sent, with no resend.
    await goOffline();
    await goOnline();
    await passSeconds(5);
    expect(outboxItems().filter((p) => p.state !== 'sent')).toEqual([]);
    expect(api.callsTo(CONFIRM)).toHaveLength(1);
    expect(screen.queryByTestId(queuedId)).toBeNull();
    await tap(optionId(inboxIds.segment, 'waiting'));
    expect(screen.queryByTestId(inboxBtnId(ITEM, 'D10', 'approve'))).toBeNull();

    // Another reconnect once settled sends nothing; nor does a restart.
    await goOffline();
    await goOnline();
    await passSeconds(5);
    expect(api.callsTo(CONFIRM)).toHaveLength(1);
    await app.unmount();
    await renderApp({ url: URL, api });
    await screen.findByTestId(inboxRowId(ITEM));
    await passSeconds(10);
    expect(api.callsTo(CONFIRM)).toHaveLength(1);
  });

  it('clears the queued answer and the kept Inbox on sign out', async () => {
    const api = inboxApi(
      {
        [CONFIRM]: unlessDown(() => ({ status: 204 })),
        [INBOX_ROUTE]: unlessDown(() => inbox),
        'DELETE /api/auth/device-sessions/current': { status: 204 },
      },
      { inbox },
    );
    const app = await openInbox(api);
    await goOffline();
    await approveD10();
    await tap(optionId(inboxIds.segment, 'answered'));
    expect(pendingStatus()).toHaveTextContent('Sends when you’re online');

    // Sign out from the Account sheet (Projects, then the avatar).
    await tap(tabIds.rootTile);
    await tap(projectsIds.avatar);
    await fireEvent.press(await screen.findByTestId(accountIds.signOut));
    await fireEvent.press(await screen.findByTestId(accountIds.confirmYes));
    await screen.findByTestId(screenIds.signIn);
    await passSeconds(5);
    await app.unmount();

    // The next sign-in: back online, the Inbox held, nothing is sent and no row shows before the fetch.
    await goOnline();
    api.hang(INBOX_ROUTE);
    await renderApp({ url: URL, api });
    await waitFor(() => expect(api.callsTo(INBOX_ROUTE)).toHaveLength(2));
    await passSeconds(10);
    expect(screen.queryByTestId(inboxRowId(ITEM))).toBeNull();
    expect(api.callsTo(CONFIRM)).toHaveLength(0);
    api.release(INBOX_ROUTE);
  });
});
