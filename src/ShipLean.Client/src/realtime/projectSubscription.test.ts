import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ConnectionState } from './connectionState';
import { fakeConnection } from './testing/fakeConnection';
import { createProjectSubscription } from './projectSubscription';

const project = '0199a000-0000-7000-8000-000000000012';

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

function subscribe(hub: ReturnType<typeof fakeConnection>, sessionOk = false) {
  const states: ConnectionState[] = [];
  const reconnected: (string | undefined)[] = [];
  const refusals: string[] = [];
  const subscription = createProjectSubscription({
    connect: () => hub.connection,
    retryDelaysMs: [0, 100, 500],
    onEvent: () => undefined,
    onState: (state) => states.push(state),
    onReset: () => undefined,
    onReconnected: (id) => reconnected.push(id),
    confirmSession: async () => {
      refusals.push('checked');
      return sessionOk;
    },
  });
  return { subscription, states, reconnected, refusals };
}

test('subscription_WhenDropped_ShouldBackOffAndRejoin', async () => {
  const hub = fakeConnection(['ok', 'fail', 'fail', 'ok']);
  const { subscription, states, reconnected } = subscribe(hub);
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  expect(hub.calls).toEqual([`JoinProject ${project}`]);
  expect(states.at(-1)).toBe('live');

  hub.drop();
  expect(states.at(-1)).toBe('reconnecting');
  await vi.advanceTimersByTimeAsync(0);
  expect(hub.spies.start).toHaveBeenCalledTimes(2);
  await vi.advanceTimersByTimeAsync(99);
  expect(hub.spies.start).toHaveBeenCalledTimes(2);
  await vi.advanceTimersByTimeAsync(1);
  expect(hub.spies.start).toHaveBeenCalledTimes(3);
  expect(hub.calls).toEqual([`JoinProject ${project}`]);
  await vi.advanceTimersByTimeAsync(500);

  expect(hub.spies.start).toHaveBeenCalledTimes(4);
  expect(hub.calls).toEqual([`JoinProject ${project}`, `JoinProject ${project}`]);
  expect(states.at(-1)).toBe('live');
  expect(reconnected).toEqual([project]);
});

test('subscription_WhenRejoined_ShouldRejoinScreensToo', async () => {
  const hub = fakeConnection();
  const { subscription } = subscribe(hub);
  // The screen is asked for first, as React runs a child's effect before its parent's.
  subscription.setScreen('storiesBacklog');
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  expect(hub.calls).toEqual([`JoinProject ${project}`, `JoinScreen ${project} storiesBacklog`]);

  hub.drop();
  await vi.advanceTimersByTimeAsync(0);
  expect(hub.calls.slice(2)).toEqual([
    `JoinProject ${project}`,
    `JoinScreen ${project} storiesBacklog`,
  ]);

  subscription.setScreen('sprints');
  await vi.advanceTimersByTimeAsync(0);
  expect(hub.calls.slice(4)).toEqual([
    `LeaveScreen ${project} storiesBacklog`,
    `JoinScreen ${project} sprints`,
  ]);
});

test('subscription_WhenFirstConnectFails_ShouldReportOfflineAndKeepTrying', async () => {
  const hub = fakeConnection(['fail', 'ok']);
  const { subscription, states } = subscribe(hub);
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  expect(states.at(-1)).toBe('offline');
  await vi.advanceTimersByTimeAsync(100);
  expect(states.at(-1)).toBe('live');
  expect(hub.calls).toEqual([`JoinProject ${project}`]);
});

test('subscription_WhenProjectRefused_ShouldNotAskAgainOrJoinItsScreen', async () => {
  const hub = fakeConnection();
  hub.spies.invoke.mockRejectedValueOnce(
    new Error(
      "An unexpected error occurred invoking 'JoinProject' on the server. HubException: not_found: x",
    ),
  );
  const { subscription } = subscribe(hub);
  subscription.setScreen('sprints');
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  expect(hub.spies.invoke).toHaveBeenCalledTimes(1);
});

test('subscription_WhenEventForAnotherProject_ShouldNotPassIt', async () => {
  const hub = fakeConnection();
  const received: unknown[] = [];
  const subscription = createProjectSubscription({
    connect: () => hub.connection,
    onEvent: (event) => received.push(event),
    onState: () => undefined,
    onReset: () => undefined,
    onReconnected: () => undefined,
    confirmSession: async () => true,
  });
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  hub.deliver({ type: 'projectChanged', projectId: project.toUpperCase(), clientId: null });
  hub.deliver({ type: 'projectChanged', projectId: 'other', clientId: null });
  hub.deliver('nonsense');
  expect(received).toHaveLength(1);
});

// What SignalR throws when negotiate answers 401/403: no statusCode, only the message.
const negotiateFailure = (status: number) =>
  new Error(
    `Failed to complete negotiation with the server: Error: ${status === 401 ? 'Unauthorized' : 'Forbidden'}: Status code '${status}'`,
  );

async function dropThenRefuse(hub: ReturnType<typeof fakeConnection>, error: unknown, ok = false) {
  hub.spies.start.mockImplementationOnce(async () => undefined);
  const setup = subscribe(hub, ok);
  setup.subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  hub.spies.start.mockRejectedValue(error);
  hub.drop();
  await vi.advanceTimersByTimeAsync(40_000);
  return setup;
}

test.each([401, 403])(
  'subscription_WhenReconnectNegotiatesWith%i_ShouldCheckSessionAndStopWhenOver',
  async (status) => {
    const hub = fakeConnection(['ok']);
    const { refusals } = await dropThenRefuse(hub, negotiateFailure(status));
    expect(refusals).toEqual(['checked']);
    expect(hub.spies.start).toHaveBeenCalledTimes(2);
  },
);

test('subscription_WhenRefusalCarriesStatusCode_ShouldCheckSessionToo', async () => {
  const hub = fakeConnection(['ok']);
  const { refusals } = await dropThenRefuse(
    hub,
    Object.assign(new Error('Unauthorized'), { statusCode: 401 }),
  );
  expect(refusals).toEqual(['checked']);
});

test('subscription_When403ButSessionIsFine_ShouldStayOfflineAndKeepRetrying', async () => {
  const hub = fakeConnection(['ok']);
  const { states, refusals } = await dropThenRefuse(hub, negotiateFailure(403), true);
  expect(states.at(-1)).toBe('offline');
  expect(refusals.length).toBeGreaterThan(1);
  expect(hub.spies.start.mock.calls.length).toBeGreaterThan(2);
});

test('subscription_WhenFirstConnectSucceedsAfterAFailure_ShouldCountAsReconnect', async () => {
  const hub = fakeConnection(['fail', 'ok']);
  const { subscription, reconnected } = subscribe(hub);
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(100);
  expect(hub.calls).toEqual([`JoinProject ${project}`]);
  expect(reconnected).toEqual([project]);
});

test('subscription_WhenFirstConnectSucceeds_ShouldNotCountAsReconnect', async () => {
  const hub = fakeConnection(['ok']);
  const { subscription, reconnected } = subscribe(hub);
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  expect(reconnected).toEqual([]);
});

const hubError = (method: string, code: string) =>
  new Error(
    `An unexpected error occurred invoking '${method}' on the server. HubException: ${code}: x`,
  );

test.each(['password_change_required', 'session_ended'])(
  'subscription_WhenHubRefuses%s_ShouldCheckSessionAndStop',
  async (code) => {
    const hub = fakeConnection();
    hub.spies.invoke.mockRejectedValueOnce(hubError('JoinProject', code));
    const { subscription, refusals, states } = subscribe(hub);
    subscription.setProject(project);
    await vi.advanceTimersByTimeAsync(0);
    expect(refusals).toEqual(['checked']);
    expect(states.at(-1)).toBe('offline');
  },
);

test('subscription_WhenJoinProjectNotFound_ShouldBeOfflineUntilProjectChanges', async () => {
  const hub = fakeConnection();
  hub.spies.invoke.mockRejectedValueOnce(hubError('JoinProject', 'not_found'));
  const { subscription, states, refusals } = subscribe(hub);
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  expect(states.at(-1)).toBe('offline');
  expect(refusals).toEqual([]);
  subscription.setProject('0199a000-0000-7000-8000-000000000013');
  await vi.advanceTimersByTimeAsync(0);
  expect(states.at(-1)).toBe('live');
});
