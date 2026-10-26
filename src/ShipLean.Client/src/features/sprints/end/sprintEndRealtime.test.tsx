import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '../../../api/projects';
import { fakeConnection } from '../../../realtime/testing/fakeConnection';

const hub = fakeConnection();
vi.mock('../../../realtime/connection', async (original) => ({
  ...(await original<typeof import('../../../realtime/connection')>()),
  createProjectConnection: () => hub.connection,
}));
const reads = vi.hoisted(() => ({ paths: [] as string[] }));
vi.mock('../../../api/client', async (original) => ({
  ...(await original<typeof import('../../../api/client')>()),
  api: {
    get: vi.fn(async (path: string) => {
      reads.paths.push(path);
      return {};
    }),
  },
}));

const { useProjectRealtime } = await import('../../../realtime/useProjectRealtime');
const { useRollover, useSprintEndRealtime } = await import('../sprintsApi');
const { projectSprintsKey } = await import('../../../queries/shared');
const { useQuery } = await import('@tanstack/react-query');

const projectId = '0199a000-0000-7000-8000-000000000012';

function Screen() {
  useProjectRealtime('SHIP');
  useSprintEndRealtime(projectId);
  useRollover(projectId, 's24');
  useQuery({
    queryKey: projectSprintsKey(projectId),
    queryFn: async () => {
      reads.paths.push('/sprints');
      return [];
    },
  });
  return null;
}

let root: Root;
const count = (suffix: string) => reads.paths.filter((p) => p.endsWith(suffix)).length;
beforeEach(async () => {
  vi.useFakeTimers();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  reads.paths = [];
  const queryClient = new QueryClient({
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

const events = [
  { type: 'sprintChanged', sprintId: 's24' },
  { type: 'workItemCreated', id: 'i', key: 'SHIP-1', workItemType: 'story', parentId: null },
  { type: 'workItemUpdated', id: 'i', revision: 2, fields: ['state'], pulled: false },
  { type: 'workItemRemoved', id: 'i', parentId: null },
  { type: 'projectChanged' },
] satisfies Record<string, unknown>[];

test.each(events)(
  'useSprintEndRealtime_When$type, ShouldRefetchTheSprintsAndTheRollover',
  async (event) => {
    expect(count('/rollover')).toBe(1);
    expect(count('/sprints')).toBe(1);
    await deliver(event);
    expect(count('/rollover')).toBe(2);
    expect(count('/sprints')).toBe(2);
  },
);

test('useSprintEndRealtime_WhenAnUnrelatedEventArrives_ShouldNotRefetch', async () => {
  await deliver({ type: 'presenceChanged', screenId: 'backlog', userIds: [], sequence: 1 });
  expect(count('/rollover')).toBe(1);
  expect(count('/sprints')).toBe(1);
});
