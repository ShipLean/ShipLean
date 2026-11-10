import type { AnsweredDto, InboxDto } from '@shiplean/core/api/inbox';
import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { aProject } from '../test-support/builders';
import type { ApiHandler } from '../test-support/fakeApi';
import { anAnswered, anAnsweredRow, inboxApi } from '../test-support/inbox';
import { designInbox } from '../test-support/inboxDesign';
import { setConnected } from '../test-support/netInfo';
import { renderApp } from '../test-support/renderApp';
import { kvValues } from '../test-support/storage';
import { accountIds } from '../screens/account/testIds';
import { projectsIds } from '../screens/projects/testIds';
import { screenIds, tabIds } from '../screens/testIds';
import { optionId } from '../ui/testIds';
import {
  answeredNextId,
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
// route get no response, as fetch does with no connection. The projects list stays reachable: it is not kept on the
// phone, so a restart with every route down cannot even open the project (reported, not worked around here).
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
const pendingStatus = () => screen.getByTestId(/^answered-pending-status-/);

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
    const queuedId = pendingStatus().props.testID as string;

    // 2. Restart over the same kv store, with the connection back but the Inbox and the send held at the server (a
    // restart with no connection at all cannot open the project: see the comment on `down`). renderApp empties the
    // query cache, so the row can only be the one kept on the phone, and the answer only the one in the outbox.
    await first.unmount();
    await goOnline();
    api.hang(INBOX_ROUTE);
    api.hang(CONFIRM);
    await renderApp({ url: URL, api });
    expect(await screen.findByTestId(inboxRowId(ITEM))).toBeOnTheScreen();
    await tap(optionId(inboxIds.segment, 'answered'));
    expect(screen.getByTestId(queuedId)).toHaveTextContent('Sending…');
    await waitFor(() => expect(api.callsTo(CONFIRM)).toHaveLength(1));

    // 3. The send answers: it went out exactly once, and the restart did not queue a second.
    api.release(INBOX_ROUTE);
    api.release(CONFIRM);
    await passSeconds(5);
    expect(api.callsTo(CONFIRM)).toHaveLength(1);
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
    const loads = api.callsTo(INBOX_ROUTE).length;

    await approveD10();
    await passSeconds(5);

    // It is uncertain: sent once, no response, nothing resent.
    expect(api.callsTo(CONFIRM)).toHaveLength(1);
    await tap(optionId(inboxIds.segment, 'answered'));
    expect(pendingStatus()).toHaveTextContent('Sending…');

    // The connection returns: reconcile reads a fresh Inbox, sees D10 closed and settles it as sent, with no resend.
    await goOffline();
    await goOnline();
    await passSeconds(5);
    expect(api.callsTo(INBOX_ROUTE).length).toBeGreaterThan(loads);
    expect(api.callsTo(CONFIRM)).toHaveLength(1);
    expect(screen.queryByTestId(/^answered-pending-status-/)).toBeNull();
    await tap(optionId(inboxIds.segment, 'waiting'));
    expect(screen.queryByTestId(inboxBtnId(ITEM, 'D10', 'approve'))).toBeNull();

    // A restart sends nothing more.
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
