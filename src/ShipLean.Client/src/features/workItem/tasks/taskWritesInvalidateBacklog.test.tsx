import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ChildSummaryDto } from '@shiplean/core/api/workItems';
import { backlogKey } from '../../../queries/backlogKeys';
import { story } from '../ct/fixtures';

vi.mock('@shiplean/core/api/client', async (original) => ({
  ...(await original<typeof import('@shiplean/core/api/client')>()),
  api: {
    get: vi.fn(async () => ({ ...story, revision: 4 })),
    patch: vi.fn(async () => ({
      id: story.id,
      key: story.key,
      revision: 5,
      changes: [],
      notices: [],
    })),
    post: vi.fn(async () => ({
      id: 'task-1',
      key: 'SHIP-200',
      revision: 1,
      changes: [],
      notices: [],
    })),
  },
}));

const { useTaskState } = await import('./useTaskState');
const { useAddTask } = await import('./useAddTask');

let root: Root;
let queryClient: QueryClient;
const hooks = {} as {
  state: ReturnType<typeof useTaskState>;
  add: ReturnType<typeof useAddTask>;
};
function Probe() {
  const state = useTaskState(story.projectId, story.key);
  const add = useAddTask(story.projectId, story.key, story);
  useEffect(() => {
    hooks.state = state;
    hooks.add = add;
  });
  return null;
}

beforeEach(async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  root = createRoot(document.createElement('div'));
  await act(async () => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <Probe />
      </QueryClientProvider>,
    );
  });
});
afterEach(() => act(() => root.unmount()));

const rows = backlogKey(story.projectId, 'stories', 'list', '', false);

test('Changing_state_in_the_modal_invalidates_the_backlog', async () => {
  queryClient.setQueryData(rows, { rows: [] });

  await act(async () => {
    await hooks.state.mutateAsync({
      task: { key: 'SHIP-200' } as ChildSummaryDto,
      stateId: 's-active',
    });
  });

  expect(queryClient.getQueryState(rows)?.isInvalidated).toBe(true);
});

test('Adding_a_task_in_the_modal_invalidates_the_backlog', async () => {
  queryClient.setQueryData(rows, { rows: [] });

  await act(async () => {
    await hooks.add.mutateAsync('Write the tests');
  });

  expect(queryClient.getQueryState(rows)?.isInvalidated).toBe(true);
});
