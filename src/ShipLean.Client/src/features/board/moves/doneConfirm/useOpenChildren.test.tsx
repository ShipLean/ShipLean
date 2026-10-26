import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '../../../../api/projects';
import { fakeConnection } from '../../../../realtime/testing/fakeConnection';

const hub = fakeConnection();
vi.mock('../../../../realtime/connection', async (original) => ({
  ...(await original<typeof import('../../../../realtime/connection')>()),
  createProjectConnection: () => hub.connection,
}));
const reads = vi.hoisted(() => ({ count: 0 }));
vi.mock('../../../../api/client', async (original) => ({
  ...(await original<typeof import('../../../../api/client')>()),
  api: {
    get: vi.fn(async () => {
      reads.count += 1;
      return [];
    }),
  },
}));

const { useProjectRealtime } = await import('../../../../realtime/useProjectRealtime');
const { useOpenChildren } = await import('./useOpenChildren');

const projectId = '0199a000-0000-7000-8000-000000000012';

function Screen() {
  useProjectRealtime('SHIP');
  useOpenChildren(projectId, 'item-1');
  return null;
}

let root: Root;
beforeEach(async () => {
  vi.useFakeTimers();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  reads.count = 0;
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

test.each([
  { type: 'workItemCreated', id: 'c', key: 'SHIP-1', workItemType: 'story', parentId: 'item-1' },
  { type: 'workItemUpdated', id: 'c', revision: 2, fields: ['state'], pulled: false },
  { type: 'workItemMoved', id: 'c', parentId: 'item-1' },
  { type: 'workItemRemoved', id: 'c', parentId: 'item-1' },
  { type: 'statesChanged' },
])('useOpenChildren_WhenEvent_$type_ShouldRefetchTheChildren', async (event) => {
  expect(reads.count).toBe(1);
  await deliver(event);
  expect(reads.count).toBe(2);
});

test('useOpenChildren_WhenAnUnrelatedEventArrives_ShouldNotRefetch', async () => {
  await deliver({ type: 'sprintChanged', sprintId: 's1' });
  expect(reads.count).toBe(1);
});
