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
const reads = vi.hoisted(() => ({ count: 0 }));
vi.mock('../../../api/client', async (original) => ({
  ...(await original<typeof import('../../../api/client')>()),
  api: {
    get: vi.fn(async () => {
      reads.count += 1;
      return { id: 'parent', children: [{ id: 'child' }] };
    }),
  },
}));

const { useProjectRealtime } = await import('../../../realtime/useProjectRealtime');
const { useWorkItem } = await import('../workItemApi');
const { useChildEvents } = await import('./useChildEvents');

const projectId = '0199a000-0000-7000-8000-000000000012';

function Screen() {
  useProjectRealtime('SHIP');
  const item = useWorkItem(projectId, 'SHIP-1').data;
  useChildEvents(projectId, 'SHIP-1', item);
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

// Delivers one event from a teammate (no clientId) and says whether the item was read again.
async function refetched(event: Record<string, unknown>) {
  const before = reads.count;
  hub.deliver({ projectId, clientId: null, ...event });
  await act(() => vi.advanceTimersByTimeAsync(0));
  return reads.count > before;
}

test('useChildEvents_WhenTaskCreatedUnderTheItem_ShouldRefetchIt', async () => {
  expect(
    await refetched({
      type: 'workItemCreated',
      id: 'new',
      key: 'SHIP-9',
      workItemType: 'task',
      parentId: 'parent',
    }),
  ).toBe(true);
});

test('useChildEvents_WhenTaskRemovedFromTheItem_ShouldRefetchIt', async () => {
  expect(await refetched({ type: 'workItemRemoved', id: 'gone', parentId: 'parent' })).toBe(true);
});

test.each([
  { type: 'workItemUpdated', id: 'child', revision: 2, fields: ['state'], pulled: false },
  {
    type: 'workItemMoved',
    id: 'child',
    revision: 2,
    columnId: null,
    done: false,
    laneId: null,
    columnOrder: 'a',
    stateId: 's',
    parentId: 'parent',
  },
])('useChildEvents_WhenChildEvent$type_ShouldRefetchTheItem', async (event) => {
  expect(await refetched(event)).toBe(true);
});

test('useChildEvents_WhenTaskMovedOntoTheItem_ShouldRefetchIt', async () => {
  expect(
    await refetched({
      type: 'workItemMoved',
      id: 'newcomer',
      revision: 2,
      columnId: null,
      done: false,
      laneId: null,
      columnOrder: 'a',
      stateId: 's',
      parentId: 'parent',
    }),
  ).toBe(true);
});

test('useChildEvents_WhenAnItemIsReparentedFromItsOwnForm_ShouldRefetchIt', async () => {
  expect(
    await refetched({
      type: 'workItemUpdated',
      id: 'newcomer',
      revision: 2,
      fields: ['parent'],
      pulled: false,
    }),
  ).toBe(true);
});

test('useChildEvents_WhenMovedUnderAnotherItem_ShouldNotRefetch', async () => {
  expect(
    await refetched({
      type: 'workItemMoved',
      id: 'stranger',
      revision: 2,
      columnId: null,
      done: false,
      laneId: null,
      columnOrder: 'a',
      stateId: 's',
      parentId: 'other',
    }),
  ).toBe(false);
});

test.each([
  { type: 'workItemCreated', id: 'x', key: 'SHIP-8', workItemType: 'task', parentId: 'other' },
  { type: 'workItemRemoved', id: 'x', parentId: 'other' },
  { type: 'workItemUpdated', id: 'stranger', revision: 2, fields: ['title'], pulled: false },
])('useChildEvents_WhenUnrelatedEvent$type_ShouldNotRefetch', async (event) => {
  expect(await refetched(event)).toBe(false);
});
