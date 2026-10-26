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
const { fetchSprintCapacity, useCapacityRealtime } = await import('../sprintsApi');
const { useQuery } = await import('@tanstack/react-query');
const { fetchMembers, projectMembersKey } = await import('../../../queries/shared');

const projectId = '0199a000-0000-7000-8000-000000000012';

function Screen() {
  useProjectRealtime('SHIP');
  useCapacityRealtime(projectId);
  useQuery({ queryKey: projectMembersKey(projectId), queryFn: () => fetchMembers(projectId) });
  useQuery({
    queryKey: ['projects', projectId, 'sprint-capacity', 's1'],
    queryFn: () => fetchSprintCapacity('s1'),
  });
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

test('useCapacityRealtime_WhenProjectChanged_ShouldRefetchTheMembers', async () => {
  expect(reads_('/members')).toBe(1);
  await deliver({ type: 'projectChanged' });
  expect(reads_('/members')).toBe(2);
});

test('useCapacityRealtime_WhenThisSprintChanged_ShouldRefetchItsCapacityNotTheMembers', async () => {
  await deliver({ type: 'sprintChanged', sprintId: 's1' });
  expect(reads_('/capacity')).toBe(2);
  expect(reads_('/members')).toBe(1);
});

test('useCapacityRealtime_WhenAnotherSprintChanged_ShouldLeaveThisCapacityAlone', async () => {
  await deliver({ type: 'sprintChanged', sprintId: 's2' });
  expect(reads_('/capacity')).toBe(1);
});
