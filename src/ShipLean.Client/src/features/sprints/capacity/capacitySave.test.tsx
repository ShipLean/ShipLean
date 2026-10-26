import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { api } from '../../../api/client';
import type { SprintCapacityDto } from '../../../api/sprints';
import { backlogRootKey } from '../../backlog/backlogApi';
import { projectSprintsKey } from '../../../queries/shared';

const calls = vi.hoisted(() => ({ reads: [] as string[], saved: null as unknown }));
vi.mock('../../../api/client', async (original) => ({
  ...(await original<typeof import('../../../api/client')>()),
  api: {
    get: vi.fn(async (path: string) => {
      calls.reads.push(path);
      return {};
    }),
    put: vi.fn(async () => calls.saved),
  },
}));

const { useSaveSprintCapacity } = await import('../sprintsApi');

const projectId = 'p1';
const saved: SprintCapacityDto = { sprintId: 's1', teamTotal: 0, members: [] };
let root: Root;
let host: HTMLElement;

function Screen() {
  useQuery({
    queryKey: [...backlogRootKey(projectId), 'x'],
    queryFn: () => api.get('/backlog'),
  });
  useQuery({
    queryKey: projectSprintsKey(projectId),
    queryFn: () => api.get('/sprints'),
  });
  const save = useSaveSprintCapacity(projectId, 's1');
  return <button type="button" onClick={() => save.mutate({ members: [] })} />;
}

beforeEach(async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  calls.reads = [];
  calls.saved = saved;
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  host = document.createElement('div');
  root = createRoot(host);
  await act(async () => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <Screen />
      </QueryClientProvider>,
    );
  });
});
afterEach(() => {
  act(() => root.unmount());
});

const reads = (path: string) => calls.reads.filter((p) => p === path).length;

test('useSaveSprintCapacity_WhenSaved_ShouldRefetchTheBacklogAndTheSprintList', async () => {
  expect(reads('/backlog')).toBe(1);
  expect(reads('/sprints')).toBe(1);
  await act(async () => {
    host.querySelector('button')?.click();
  });
  await vi.waitFor(() => expect(reads('/backlog')).toBe(2));
  expect(reads('/backlog')).toBe(2);
  expect(reads('/sprints')).toBe(2);
});
