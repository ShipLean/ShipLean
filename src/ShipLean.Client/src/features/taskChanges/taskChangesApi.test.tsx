import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { fakeConnection } from '../../realtime/testing/fakeConnection';

const hub = fakeConnection();
vi.mock('../../realtime/connection', async (original) => ({
  ...(await original<typeof import('../../realtime/connection')>()),
  createProjectConnection: () => hub.connection,
}));
const reads = vi.hoisted(() => ({ paths: [] as string[] }));
vi.mock('@shiplean/core/api/client', async (original) => ({
  ...(await original<typeof import('@shiplean/core/api/client')>()),
  api: {
    get: vi.fn(async (path: string) => {
      reads.paths.push(path);
      return {};
    }),
    post: vi.fn(async () => ({})),
  },
}));

const { useProjectRealtime } = await import('../../realtime/useProjectRealtime');
const changeReads = () => reads.paths.filter((path) => path.includes('/changes'));
const { useTaskChanges } = await import('./taskChangesApi');

const projectId = '0199a000-0000-7000-8000-000000000012';
const workItemId = 'w142';
const probe: { latest: ReturnType<typeof useTaskChanges> | undefined } = { latest: undefined };

function Screen() {
  useProjectRealtime('SHIP');
  const value = useTaskChanges(projectId, workItemId, 'all');
  useEffect(() => {
    probe.latest = value;
  });
  return null;
}

let root: Root;
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

test('useTaskChanges_WhenCommitsChanged_ShouldSetNewCommitWithoutRefetching', async () => {
  expect(changeReads()).toEqual([`/api/work-items/${workItemId}/changes?commit=all`]);
  expect(probe.latest!.newCommit).toBe(false);

  await deliver({ type: 'commitsChanged', workItemId });

  expect(probe.latest!.newCommit).toBe(true);
  expect(changeReads()).toHaveLength(1);
});

test('useTaskChanges_WhenAnotherItemsCommitsChanged_ShouldIgnoreIt', async () => {
  await deliver({ type: 'commitsChanged', workItemId: 'other' });

  expect(probe.latest!.newCommit).toBe(false);
});

test('useTaskChanges_WhenReloaded_ShouldRefetchAndClearNewCommit', async () => {
  await deliver({ type: 'commitsChanged', workItemId });

  act(() => probe.latest!.reload());
  await act(() => vi.advanceTimersByTimeAsync(0));

  expect(probe.latest!.newCommit).toBe(false);
  expect(changeReads()).toHaveLength(2);
});

test('useTaskChanges_WhenCodeCommentsChanged_ShouldRefetch', async () => {
  await deliver({ type: 'codeCommentsChanged', workItemId });

  expect(changeReads()).toHaveLength(2);
});
