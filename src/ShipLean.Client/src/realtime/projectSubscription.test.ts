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

test.each(['JoinProject', 'JoinScreen'])(
  'subscription_When%sIsPending_ShouldNotReportLiveUntilItIsDone',
  async (pending) => {
    const hub = fakeConnection();
    let done: () => void = () => undefined;
    hub.spies.invoke.mockImplementation(async (method: string) => {
      if (method === pending) await new Promise<void>((resolve) => (done = resolve));
    });
    const { subscription, states } = subscribe(hub);
    subscription.setScreen('sprints');
    subscription.setProject(project);
    await vi.advanceTimersByTimeAsync(0);
    expect(hub.spies.start).toHaveBeenCalledTimes(1);
    expect(states).not.toContain('live');

    done();
    await vi.advanceTimersByTimeAsync(0);
    expect(states.at(-1)).toBe('live');
  },
);

test('subscription_WhenJoinProjectFailsOtherwise_ShouldReportOfflineNotLive', async () => {
  const hub = fakeConnection();
  hub.spies.invoke.mockRejectedValueOnce(hubError('JoinProject', 'internal_error'));
  const { subscription, states } = subscribe(hub);
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  expect(states).not.toContain('live');
  expect(states.at(-1)).toBe('offline');
});

test('subscription_WhenJoinScreenIsRefused_ShouldStillReportLive', async () => {
  const hub = fakeConnection();
  hub.spies.invoke.mockImplementation(async (method: string) => {
    if (method === 'JoinScreen') throw hubError('JoinScreen', 'validation');
  });
  const { subscription, states } = subscribe(hub);
  subscription.setScreen('sprints');
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  expect(states.at(-1)).toBe('live');
});

// Hub calls that wait until the test answers them, in the order they were made.
function deferredHub() {
  const hub = fakeConnection();
  const pending: { method: string; args: unknown[]; ok: () => void; fail: (e: Error) => void }[] =
    [];
  hub.spies.invoke.mockImplementation(
    (method: string, ...args: unknown[]) =>
      new Promise<void>((resolve, reject) => {
        hub.calls.push([method, ...args].join(' '));
        pending.push({ method, args, ok: resolve, fail: reject });
      }),
  );
  const take = (call: string) => {
    const index = pending.findIndex((p) => [p.method, ...p.args].join(' ') === call);
    expect(index, `${call} is pending`).toBeGreaterThanOrEqual(0);
    return pending.splice(index, 1)[0]!;
  };
  return { hub, take, pending };
}

const projectB = '0199a000-0000-7000-8000-000000000013';

test('subscription_WhenProjectChanges_ShouldLeaveLiveUntilTheNewProjectIsJoined', async () => {
  const { hub, take } = deferredHub();
  const { subscription, states } = subscribe(hub);
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  take(`JoinProject ${project}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states.at(-1)).toBe('live');

  subscription.setProject(projectB);
  expect(states.at(-1)).not.toBe('live');
  take(`LeaveProject ${project}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states.at(-1)).not.toBe('live');
  take(`JoinProject ${projectB}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states.at(-1)).toBe('live');
});

test('subscription_WhenSwitchedAToBToAWhileJoinsPending_ShouldBeLiveOnlyOnceAIsJoined', async () => {
  const { hub, take, pending } = deferredHub();
  const { subscription, states } = subscribe(hub);
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  take(`JoinProject ${project}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states.at(-1)).toBe('live');

  subscription.setProject(projectB);
  subscription.setProject(project);
  const marker = states.length;
  take(`LeaveProject ${project}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states.slice(marker)).not.toContain('live');
  expect(states.at(-1)).not.toBe('live');
  take(`JoinProject ${project}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states.at(-1)).toBe('live');
  expect(pending).toEqual([]);
});

test('subscription_WhenOldJoinAnswersLate_ShouldNotSetLiveOrMarkTheWrongProjectJoined', async () => {
  const { hub, take, pending } = deferredHub();
  const { subscription, states } = subscribe(hub);
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  // The join of A is still on its way when B is wanted.
  subscription.setProject(projectB);
  take(`JoinProject ${project}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states).not.toContain('live');
  // A is left (it was joined), and only then is B joined.
  expect(hub.calls.slice(1)).toEqual([`LeaveProject ${project}`]);
  take(`LeaveProject ${project}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states).not.toContain('live');
  take(`JoinProject ${projectB}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states.at(-1)).toBe('live');
  expect(pending).toEqual([]);
});

test('subscription_WhenOldJoinIsRefusedLate_ShouldNotTakeLiveOrOfflineFromTheNewProject', async () => {
  const { hub, take } = deferredHub();
  const { subscription, states } = subscribe(hub);
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  subscription.setProject(projectB);
  take(`JoinProject ${project}`).fail(hubError('JoinProject', 'not_found'));
  await vi.advanceTimersByTimeAsync(0);
  expect(states).not.toContain('offline');
  take(`JoinProject ${projectB}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states.at(-1)).toBe('live');
});

test('subscription_WhenProjectIsDroppedAndAnotherWanted_ShouldNotBeLiveBeforeTheJoin', async () => {
  const { hub, take } = deferredHub();
  const { subscription, states } = subscribe(hub);
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  take(`JoinProject ${project}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  // A page's effect cleanup asks for no project, then the next one.
  subscription.setProject(undefined);
  subscription.setProject(projectB);
  const marker = states.length;
  take(`LeaveProject ${project}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states.slice(marker)).not.toContain('live');
  take(`JoinProject ${projectB}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states.at(-1)).toBe('live');
});

test('subscription_WhenScreenJoinIsPendingAfterProjectChange_ShouldHoldLive', async () => {
  const { hub, take } = deferredHub();
  const { subscription, states } = subscribe(hub);
  subscription.setScreen('sprints');
  subscription.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  take(`JoinProject ${project}`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states).not.toContain('live');
  take(`JoinScreen ${project} sprints`).ok();
  await vi.advanceTimersByTimeAsync(0);
  expect(states.at(-1)).toBe('live');
});
