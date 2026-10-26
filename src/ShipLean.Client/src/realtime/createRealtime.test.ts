import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { createRealtime } from './createRealtime';
import { fakeConnection } from './testing/fakeConnection';

const project = '0199a000-0000-7000-8000-000000000012';

beforeEach(() => vi.useFakeTimers());
afterEach(() => vi.useRealTimers());

test('reconnect_ShouldReportInvalidatedCount', async () => {
  const hub = fakeConnection();
  const realtime = createRealtime({
    connect: () => hub.connection,
    ownClientId: 'tab-1',
    confirmSession: async () => true,
    retryDelaysMs: [0],
  });
  const invalidated: string[] = [];
  realtime.setInvalidator((id) => {
    invalidated.push(id);
    return 4;
  });
  const reported: number[] = [];
  realtime.onReconnected((count) => reported.push(count));

  realtime.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  expect(reported).toEqual([]);

  hub.drop();
  await vi.advanceTimersByTimeAsync(0);

  expect(invalidated).toEqual([project]);
  expect(reported).toEqual([4]);
});

test('presence_WhenListed_ShouldKeepItPerScreenAndClearOnDrop', async () => {
  const hub = fakeConnection();
  const realtime = createRealtime({
    connect: () => hub.connection,
    ownClientId: 'tab-1',
    confirmSession: async () => true,
    retryDelaysMs: [0],
  });
  realtime.setProject(project);
  await vi.advanceTimersByTimeAsync(0);

  hub.deliver({
    type: 'presenceChanged',
    projectId: project,
    clientId: null,
    screenId: 'sprints',
    userIds: ['u1'],
    sequence: 1,
  });
  hub.deliver({
    type: 'presenceChanged',
    projectId: project,
    clientId: null,
    screenId: '<script>',
    userIds: ['u2'],
    sequence: 2,
  });
  expect(realtime.presence.get().get('sprints')).toEqual(['u1']);
  expect(realtime.presence.get().size).toBe(1);

  hub.drop();
  expect(realtime.presence.get().size).toBe(0);
});

test('presence_WhenAnOlderListArrivesLate_ShouldKeepTheNewerOne', async () => {
  const hub = fakeConnection();
  const realtime = createRealtime({
    connect: () => hub.connection,
    ownClientId: 'tab-1',
    confirmSession: async () => true,
    retryDelaysMs: [0],
  });
  realtime.setProject(project);
  await vi.advanceTimersByTimeAsync(0);
  const list = (screenId: 'sprints' | 'storiesBoard', userIds: string[], sequence: number) =>
    hub.deliver({
      type: 'presenceChanged',
      projectId: project,
      clientId: null,
      screenId,
      userIds,
      sequence,
    });

  list('sprints', ['u1', 'u2'], 8);
  list('sprints', ['u1'], 7);
  list('storiesBoard', ['u3'], 5);
  expect(realtime.presence.get().get('sprints')).toEqual(['u1', 'u2']);
  expect(realtime.presence.get().get('storiesBoard')).toEqual(['u3']);

  hub.drop();
  await vi.advanceTimersByTimeAsync(0);
  list('sprints', ['u4'], 1);
  expect(realtime.presence.get().get('sprints')).toEqual(['u4']);
});
