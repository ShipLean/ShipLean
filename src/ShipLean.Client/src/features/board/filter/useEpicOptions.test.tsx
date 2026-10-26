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
      return [{ id: 'e1', title: 'Seller payouts' }];
    }),
  },
}));

const { useProjectRealtime } = await import('../../../realtime/useProjectRealtime');
const { useEpicOptions } = await import('./useEpicOptions');

const projectId = '0199a000-0000-7000-8000-000000000012';

function Screen({ enabled }: { enabled: boolean }) {
  useProjectRealtime('SHIP');
  return <output>{useEpicOptions(projectId, enabled).length}</output>;
}

let root: Root;
let container: HTMLElement;
const mountWith = async (enabled: boolean) => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  queryClient.setQueryData(['projects', 'by-key', 'SHIP'], { id: projectId } as ProjectDto);
  act(() => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <Screen enabled={enabled} />
      </QueryClientProvider>,
    );
  });
  await act(() => vi.advanceTimersByTimeAsync(0));
};
beforeEach(() => {
  vi.useFakeTimers();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  reads.count = 0;
  container = document.createElement('div');
  root = createRoot(container);
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
  { type: 'workItemCreated', id: 'i', key: 'SHIP-1', workItemType: 'epic', parentId: null },
  { type: 'workItemUpdated', id: 'i', revision: 2, fields: ['title'], pulled: false },
  { type: 'workItemRemoved', id: 'i', parentId: null },
])('useEpicOptions_WhenEvent$type_ShouldRefetchTheEpics', async (event) => {
  await mountWith(true);
  expect(container.textContent).toBe('1');
  expect(reads.count).toBe(1);
  await deliver(event);
  expect(reads.count).toBe(2);
});

test('useEpicOptions_WhenNotEnabled_ShouldFetchNothing', async () => {
  await mountWith(false);
  await deliver({
    type: 'workItemCreated',
    id: 'i',
    key: 'SHIP-1',
    workItemType: 'epic',
    parentId: null,
  });
  expect(reads.count).toBe(0);
  expect(container.textContent).toBe('0');
});
