import { act, waitFor } from '@testing-library/react-native';
import type { ReactNode } from 'react';
import { signOutNow } from '../screens/account/signOutNow';
import { TEST_ORIGIN, aMe } from '../test-support/builders';
import { controlledApi, type ControlledApi } from '../test-support/controlledApi';
import { sessionEnded, type ApiTable } from '../test-support/fakeApi';
import { setConnected } from '../test-support/netInfo';
import { renderApp } from '../test-support/renderApp';
import { signedInApi } from '../test-support/signedInApi';
import { kvValues } from '../test-support/storage';
import { outboxKey } from './outbox';
import { scopeOf } from './scope';
import type { ActionRunner, AppAction, Executor } from './types';
import { UNDO } from './undo';

// The Retro slot is swapped for one that registers a recording retro executor and hands the test its runner, so the
// real per-session runner (shell/providers/runner.tsx) runs inside the real app. The Inbox tab doesn't read Retro.
const mockSlot: { runner: ActionRunner | null } = { runner: null };
const mockRetro: Executor<AppAction> = {
  area: 'retro',
  send: async (a, api) => {
    await api.post(`/api/test/${a.kind}/${a.itemId}`);
  },
  describe: (a) => ({ id: `${a.itemId}`, icon: 'retro', text: a.kind, removable: true }),
  reversible: () => false,
};
jest.mock('../shell/providers/retro', () => {
  const { useActionRunner } = jest.requireActual('./ActionRunnerProvider');
  return {
    RetroProvider: ({ children }: { children: ReactNode }) => {
      const runner = useActionRunner();
      runner.register(mockRetro);
      mockSlot.runner = runner;
      return children;
    },
  };
});

const URL = '/p/SHIP/inbox';
const point = (itemId: string): AppAction => ({
  area: 'retro',
  kind: 'addPoint',
  itemId,
  key: `SHIP-${itemId}`,
});
const route = (itemId: string) => `POST /api/test/addPoint/${itemId}`;
const tomsKey = outboxKey(scopeOf({ origin: TEST_ORIGIN, email: aMe().email }) as string);
const outboxKeys = () => [...kvValues.keys()].filter((k) => k.startsWith('shiplean.outbox'));

function apiWith(extra: ApiTable = {}) {
  return controlledApi({
    ...signedInApi(),
    [route('1')]: { status: 204 },
    [route('2')]: { status: 204 },
    ...extra,
  });
}

const goOffline = () => act(async () => setConnected(false));
const goOnline = () => act(async () => setConnected(true));

// The app opens online (the sign-in gate reads /api/me), and a test goes offline after that.
async function open(api: ControlledApi = apiWith()) {
  await goOnline();
  mockSlot.runner = null;
  const app = await renderApp({ url: URL, api });
  const slotRunner = (): ActionRunner | null => mockSlot.runner;
  await waitFor(() => expect(slotRunner()).not.toBeNull());
  return { ...app, runner: slotRunner() as ActionRunner };
}

async function queue(runner: ActionRunner, ...ids: string[]) {
  for (const id of ids)
    await act(async () => {
      runner.dispatch([point(id)], { undo: UNDO.none, origin: 'tap', allOrNothing: false });
    });
}

beforeEach(() => {
  for (const k of outboxKeys()) kvValues.delete(k);
});

describe('the runner across a session', () => {
  it('reloads a queued action after a remount and sends it once online', async () => {
    const api = apiWith();
    const first = await open(api);
    await goOffline();
    await queue(first.runner, '1');
    expect(first.runner.pending().map((p) => p.state)).toEqual(['queued']);
    await first.unmount();

    // The unmounted runner has stopped: coming back online sends nothing until the app is open again.
    await goOnline();
    expect(api.callsTo(route('1'))).toHaveLength(0);
    expect(outboxKeys()).toEqual([tomsKey]);

    const second = await open(api);
    await waitFor(() => expect(second.runner.pending()[0]?.state).toBe('sent'));
    expect(second.runner.pending().map((p) => p.action)).toEqual([point('1')]);
    expect(api.callsTo(route('1'))).toHaveLength(1);
    expect(outboxKeys()).toEqual([]);
  });

  it('keys the outbox by origin and email, offline too', async () => {
    const { runner } = await open();
    await goOffline();
    await queue(runner, '1');

    expect(outboxKeys()).toEqual([tomsKey]);
    expect(JSON.parse(kvValues.get(tomsKey) as string).items).toEqual([
      expect.objectContaining({ action: point('1'), state: 'queued' }),
    ]);
  });

  it('builds a new runner over the same kv values after a restart', async () => {
    const api = apiWith();
    const first = await open(api);
    await goOffline();
    await queue(first.runner, '1', '2');
    const before = first.runner.pending().map((p) => [p.id, p.batchId, p.action]);
    await first.unmount();

    api.hang(route('1'));
    const second = await open(api);
    await waitFor(() => expect(api.callsTo(route('1'))).toHaveLength(1));

    expect(second.runner).not.toBe(first.runner);
    expect(second.runner.pending().map((p) => [p.id, p.batchId, p.action])).toEqual(before);
    expect(second.runner.pending().map((p) => p.state)).toEqual(['sending', 'queued']);
    api.release(route('1'));
    await waitFor(() => expect(api.callsTo(route('2'))).toHaveLength(1));
  });

  it('clears the outbox on sign out', async () => {
    const api = apiWith();
    const { runner } = await open(api);
    await goOffline();
    await queue(runner, '1');
    expect(outboxKeys()).toEqual([tomsKey]);

    await act(() => signOutNow());
    await goOnline();

    expect(outboxKeys()).toEqual([]);
    expect(api.callsTo(route('1'))).toHaveLength(0);
  });

  it('clears the outbox when the session is lost', async () => {
    const api = apiWith({ [route('1')]: sessionEnded() });
    const { runner } = await open(api);
    await goOffline();
    await queue(runner, '1', '2');

    await goOnline();
    await waitFor(() => expect(api.callsTo(route('1'))).toHaveLength(1));

    expect(outboxKeys()).toEqual([]);
    expect(api.callsTo(route('2'))).toHaveLength(0);
  });
});
