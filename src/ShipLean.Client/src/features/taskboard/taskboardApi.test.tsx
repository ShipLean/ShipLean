import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '../../api/projects';
import { fakeConnection } from '../../realtime/testing/fakeConnection';

const hub = fakeConnection();
vi.mock('../../realtime/connection', async (original) => ({
  ...(await original<typeof import('../../realtime/connection')>()),
  createProjectConnection: () => hub.connection,
}));
const reads = vi.hoisted(() => ({ count: 0 }));
vi.mock('../../api/client', async (original) => ({
  ...(await original<typeof import('../../api/client')>()),
  api: {
    get: vi.fn(async () => {
      reads.count += 1;
      return {};
    }),
    post: vi.fn(async (...args: unknown[]) => args.length && {}),
  },
}));

const { useProjectRealtime } = await import('../../realtime/useProjectRealtime');
const { api } = await import('../../api/client');
const { taskboardKey, taskboardMoveKey, taskboardMovesPath, useCreateOnTaskboard, useTaskboard } =
  await import('./taskboardApi');

const projectId = '0199a000-0000-7000-8000-000000000012';
const sprintId = 's24';

function Screen() {
  useProjectRealtime('SHIP');
  useTaskboard(projectId, sprintId);
  return null;
}

let root: Root;
let queryClient: QueryClient;
beforeEach(async () => {
  vi.useFakeTimers();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  reads.count = 0;
  queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  queryClient.setQueryData(['projects', 'by-key', 'SHIP'], { id: projectId } as ProjectDto);
  root = createRoot(document.createElement('div'));
  act(() => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <Screen />
      </QueryClientProvider>,
    );
  });
  await act(() => vi.advanceTimersByTimeAsync(0));
});
afterEach(() => {
  act(() => root.unmount());
  vi.useRealTimers();
});

async function deliver(event: Record<string, unknown>) {
  hub.deliver({ projectId, clientId: null, ...event });
  await act(() => vi.advanceTimersByTimeAsync(0));
}

const reordered = { type: 'workItemReordered', ids: ['i'] };

test.each([
  { type: 'workItemUpdated', id: 'i', revision: 2, fields: ['title'], pulled: false },
  reordered,
])('useTaskboard_WhenEvent$type_ShouldRefetch', async (event) => {
  expect(reads.count).toBe(1);
  await deliver(event);
  expect(reads.count).toBe(2);
});

test('useTaskboard_WhenAMoveIsInFlight_ShouldNotRefetchOnATeammatesEvent', async () => {
  let finish: () => void = () => undefined;
  const move = queryClient
    .getMutationCache()
    .build(queryClient, {
      mutationKey: taskboardMoveKey(projectId, { kind: 'sprint', sprintId }),
      mutationFn: () => new Promise<void>((resolve) => (finish = resolve)),
    })
    .execute(undefined);
  await act(() => vi.advanceTimersByTimeAsync(0));

  await deliver({
    type: 'workItemUpdated',
    id: 'i',
    revision: 2,
    fields: ['title'],
    pulled: false,
  });
  expect(reads.count).toBe(1);

  finish();
  await act(() => move);
  await deliver({
    type: 'workItemUpdated',
    id: 'i',
    revision: 3,
    fields: ['title'],
    pulled: false,
  });
  expect(reads.count).toBe(2);
});

const sprintScope = { kind: 'sprint', sprintId } as const;
const featureScope = { kind: 'feature', featureId: 'f1' } as const;

test('taskboardMovesPath_ShouldFollowTheScope', () => {
  expect(taskboardMovesPath(sprintScope)).toBe('/api/sprints/s24/taskboard/moves');
  expect(taskboardMovesPath(featureScope)).toBe('/api/work-items/f1/feature-board/moves');
});

test('taskboardKey_ShouldFollowTheScope', () => {
  expect(taskboardKey(projectId, sprintScope)).toEqual([
    'projects',
    projectId,
    'taskboard',
    sprintId,
  ]);
  expect(taskboardKey(projectId, featureScope)).toEqual([
    'projects',
    projectId,
    'feature-board',
    'f1',
  ]);
  expect(taskboardMoveKey(projectId, sprintScope)).not.toEqual(
    taskboardMoveKey(projectId, featureScope),
  );
});

type Create = ReturnType<typeof useCreateOnTaskboard>['mutate'];
function Creator({
  scope,
  onReady,
}: {
  scope: typeof sprintScope | typeof featureScope;
  onReady: (mutate: Create) => void;
}) {
  const { mutate } = useCreateOnTaskboard(projectId, scope);
  useEffect(() => onReady(mutate), [mutate, onReady]);
  return null;
}

async function bodiesFor(scope: typeof sprintScope | typeof featureScope) {
  const post = vi.mocked(api.post);
  post.mockClear();
  let mutate: Create | null = null;
  const host = createRoot(document.createElement('div'));
  act(() => {
    host.render(
      <QueryClientProvider client={queryClient}>
        <Creator scope={scope} onReady={(m) => (mutate = m)} />
      </QueryClientProvider>,
    );
  });
  act(() => {
    mutate?.({ type: 'task', title: 't', parentId: 'story-1' });
    mutate?.({ type: 'story', title: 's', parentId: null });
  });
  await act(() => vi.advanceTimersByTimeAsync(0));
  act(() => host.unmount());
  return post.mock.calls.map(([, body]) => body);
}

test('useCreateOnTaskboard_WhenSprintScope_ShouldPutAStoryInTheSprintAndATaskUnderItsStory', async () => {
  expect(await bodiesFor(sprintScope)).toMatchObject([
    { type: 'task', parentId: 'story-1', sprintId: null },
    { type: 'story', parentId: null, sprintId },
  ]);
});

test('useCreateOnTaskboard_WhenFeatureScope_ShouldPutAStoryUnderTheFeatureWithNoSprint', async () => {
  expect(await bodiesFor(featureScope)).toMatchObject([
    { type: 'task', parentId: 'story-1', sprintId: null },
    { type: 'story', parentId: 'f1', sprintId: null },
  ]);
});
