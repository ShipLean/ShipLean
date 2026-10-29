import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '../../api/projects';
import type { SprintDto } from '../../api/sprints';
import { fakeConnection } from '../../realtime/testing/fakeConnection';

const hub = fakeConnection();
vi.mock('../../realtime/connection', async (original) => ({
  ...(await original<typeof import('../../realtime/connection')>()),
  createProjectConnection: () => hub.connection,
}));
const reads = vi.hoisted(() => ({ paths: [] as string[] }));
vi.mock('../../api/client', async (original) => ({
  ...(await original<typeof import('../../api/client')>()),
  api: {
    get: vi.fn(async (path: string) => {
      reads.paths.push(path);
      return {};
    }),
  },
}));

const { useProjectRealtime } = await import('../../realtime/useProjectRealtime');
const { useSprintFigures, useSprintsRealtime, useVelocity } = await import('./sprintsApi');
const { useQuery } = await import('@tanstack/react-query');
const { sprintsQuery } = await import('../../pages/projectQueries');

const projectId = '0199a000-0000-7000-8000-000000000012';
const sprints = [{ id: 's1' }] as SprintDto[];

function Screen() {
  useProjectRealtime('SHIP');
  useSprintsRealtime(projectId);
  useQuery(sprintsQuery(projectId));
  useVelocity(projectId);
  useSprintFigures(projectId, sprints);
  return null;
}

let root: Root;
const reads_ = (suffix: string) => reads.paths.filter((p) => p.endsWith(suffix)).length;
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

const refetches = [
  { type: 'sprintChanged', sprintId: 's1' },
  { type: 'projectChanged' },
  { type: 'workItemCreated', id: 'i', key: 'SHIP-1', workItemType: 'story', parentId: null },
  { type: 'workItemUpdated', id: 'i', revision: 2, fields: ['storyPoints'], pulled: false },
  {
    type: 'workItemMoved',
    id: 'i',
    revision: 2,
    columnId: 'c',
    done: false,
    laneId: null,
    columnOrder: 'a',
    stateId: 's',
    parentId: null,
  },
  { type: 'workItemRemoved', id: 'i', parentId: null },
] satisfies Record<string, unknown>[];

test.each(refetches.filter((event) => event.type !== 'sprintChanged'))(
  'useSprintsRealtime_WhenEvent$type_ShouldRefetchEverySprintFigure',
  async (event) => {
    for (const suffix of ['/sprints', '/velocity', '/metrics', '/capacity']) {
      expect(reads_(suffix)).toBe(1);
    }
    await deliver(event);
    for (const suffix of ['/sprints', '/velocity', '/metrics', '/capacity']) {
      expect(reads_(suffix)).toBe(2);
    }
  },
);

test('useSprintsRealtime_WhenAnUnrelatedEventArrives_ShouldNotRefetch', async () => {
  await deliver({ type: 'statesChanged' });
  expect(reads.paths).toHaveLength(4);
});

test('useSprintsRealtime_WhenSprintChanged_ShouldRefetchThatSprintsFigures', async () => {
  await deliver({ type: 'sprintChanged', sprintId: 's1' });
  for (const suffix of ['/sprints', '/velocity', '/metrics', '/capacity']) {
    expect(reads_(suffix)).toBe(2);
  }
});

test('useSprintsRealtime_WhenAnotherSprintChanged_ShouldLeaveThisSprintsMetricsAloneButRefetchItsCapacity', async () => {
  await deliver({ type: 'sprintChanged', sprintId: 's2' });
  expect(reads_('/sprints')).toBe(2);
  expect(reads_('/velocity')).toBe(2);
  expect(reads_('/metrics')).toBe(1);
  expect(reads_('/capacity')).toBe(2);
});
