import { inboxKeys } from '@shiplean/core/api/inbox';
import { QueryClient, dehydrate } from '@tanstack/react-query';
import { act, screen, waitFor } from '@testing-library/react-native';
import Constants from 'expo-constants';
import { queryClient } from '../queryClient';
import { isInboxQuery } from '../query/persist';
import { signOutNow } from '../screens/account/signOutNow';
import { lostSession } from '../session/lostSession';
import { TEST_ORIGIN, aMe, aProject } from '../test-support/builders';
import { anInbox, anInboxItem, inboxApi } from '../test-support/inbox';
import { renderApp } from '../test-support/renderApp';
import { kvValues } from '../test-support/storage';
import { inboxQueryStoreKey } from './storageKey';
import { inboxRowId } from './testIds';

const project = aProject();
const INBOX_ROUTE = `GET /api/projects/${project.id}/inbox`;
const KEY = inboxQueryStoreKey(TEST_ORIGIN, aMe().email) ?? '';
const END = 'DELETE /api/auth/device-sessions/current';

const kept = anInbox({
  projectId: project.id,
  items: [anInboxItem({ key: 'SHIP-901' }), anInboxItem({ key: 'SHIP-902' })],
});

// What the phone has stored after an earlier run: the Inbox of the signed-in person, as the persister writes it.
function seedKeptInbox() {
  const earlier = new QueryClient();
  earlier.setQueryData(inboxKeys.inbox(project.id), kept);
  kvValues.set(
    KEY,
    JSON.stringify({
      buster: Constants.expoConfig?.version ?? '',
      timestamp: Date.now(),
      clientState: dehydrate(earlier, { shouldDehydrateQuery: isInboxQuery }),
    }),
  );
}

// The router's test renderer runs on fake timers: moving them on is how the persister's throttle runs out.
const settle = (ms: number) => act(() => jest.advanceTimersByTime(ms));

beforeEach(() => kvValues.clear());

describe('the Inbox across restarts and sign-out', () => {
  it('keeps the loaded Inbox on the phone', async () => {
    const api = inboxApi({}, { inbox: kept });
    await renderApp({ url: '/p/SHIP/inbox', api });
    await screen.findByTestId(inboxRowId('SHIP-901'));
    await waitFor(() => expect(kvValues.get(KEY)).toContain('SHIP-901'));
    const keys = JSON.parse(kvValues.get(KEY) ?? '{}').clientState.queries.map(
      (q: { queryKey: unknown }) => q.queryKey,
    );
    expect(keys).toContainEqual(inboxKeys.inbox(project.id));
    expect(keys.every((k: unknown[]) => k[0] === 'inbox')).toBe(true);
  });

  it('shows the kept Inbox before the fetch answers after a restart', async () => {
    seedKeptInbox();
    const api = inboxApi({}, { inbox: kept });
    api.hang(INBOX_ROUTE);

    await renderApp({ url: '/p/SHIP/inbox', api });

    expect(await screen.findByTestId(inboxRowId('SHIP-901'))).toBeOnTheScreen();
    expect(screen.getByTestId(inboxRowId('SHIP-902'))).toBeOnTheScreen();
    // It asked, and the answer has not come: the rows are the kept ones.
    await waitFor(() => expect(api.callsTo(INBOX_ROUTE)).toHaveLength(1));
    api.release(INBOX_ROUTE);
  });

  it('clears the kept Inbox on sign out', async () => {
    const api = inboxApi({ [END]: { status: 204 } }, { inbox: kept });
    await renderApp({ url: '/p/SHIP/inbox', api });
    await screen.findByTestId(inboxRowId('SHIP-901'));
    await waitFor(() => expect(kvValues.has(KEY)).toBe(true));

    await act(async () => {
      await signOutNow();
    });
    // At once: the persister's own throttled save has not run yet.
    expect(kvValues.has(KEY)).toBe(false);
    await settle(900);

    expect(kvValues.has(KEY)).toBe(false);
    expect(queryClient.getQueryData(inboxKeys.inbox(project.id))).toBeUndefined();
  });

  it('clears the kept Inbox when the session is lost', async () => {
    const api = inboxApi({}, { inbox: kept });
    await renderApp({ url: '/p/SHIP/inbox', api });
    await screen.findByTestId(inboxRowId('SHIP-901'));
    await waitFor(() => expect(kvValues.has(KEY)).toBe(true));

    await act(async () => lostSession());
    // At once: the persister's own throttled save has not run yet.
    expect(kvValues.has(KEY)).toBe(false);
    await settle(900);

    expect(kvValues.has(KEY)).toBe(false);
  });

  it('shows no rows for the next sign-in before its fetch', async () => {
    seedKeptInbox();
    await renderApp({ url: '/p/SHIP/inbox', api: inboxApi({}, { inbox: kept }) });
    await screen.findByTestId(inboxRowId('SHIP-901'));
    await act(async () => lostSession());
    expect(kvValues.has(KEY)).toBe(false);
    await settle(900);
    expect(kvValues.has(KEY)).toBe(false);
    await screen.unmount();

    const next = inboxApi({}, { inbox: kept });
    next.hang(INBOX_ROUTE);
    await renderApp({ url: '/p/SHIP/inbox', api: next });
    await waitFor(() => expect(next.callsTo(INBOX_ROUTE)).toHaveLength(1));
    expect(screen.queryByTestId(inboxRowId('SHIP-901'))).toBeNull();
    next.release(INBOX_ROUTE);
  });
});
