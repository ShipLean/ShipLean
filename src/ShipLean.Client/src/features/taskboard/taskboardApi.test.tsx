import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
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
  },
}));

const { useProjectRealtime } = await import('../../realtime/useProjectRealtime');
const { taskboardMoveKey, useTaskboard } = await import('./taskboardApi');

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
      mutationKey: taskboardMoveKey(projectId, sprintId),
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
