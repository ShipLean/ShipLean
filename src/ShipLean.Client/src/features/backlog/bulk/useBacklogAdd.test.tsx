import { act, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { api } from '@shiplean/core/api/client';
import type { BacklogAddRequest } from '@shiplean/core/api/backlogWrites';
import { backlogRootKey } from '../../../queries/backlogKeys';
import { useBacklogAdd, useDuplicateCheck } from './useBacklogAdd';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
vi.mock('@shiplean/core/api/client', () => ({ api: { post: vi.fn() } }));

const post = vi.mocked(api.post);
let queryClient: QueryClient;
let add: ReturnType<typeof useBacklogAdd>;
let check: ReturnType<typeof useDuplicateCheck>;
let root: Root;

function Probe() {
  const adding = useBacklogAdd('p1', 'SHIP');
  const checking = useDuplicateCheck('p1');
  useEffect(() => {
    add = adding;
    check = checking;
  });
  return null;
}

beforeEach(() => {
  post.mockReset();
  queryClient = new QueryClient();
  root = createRoot(document.createElement('div'));
  act(() =>
    root.render(
      <QueryClientProvider client={queryClient}>
        <Probe />
      </QueryClientProvider>,
    ),
  );
});
afterEach(() => act(() => root.unmount()));

test('useBacklogAdd_WhenAdded_ShouldPostOnceAndInvalidateTheBacklogAndProject', async () => {
  const request: BacklogAddRequest = {
    items: [{ type: 'story', title: 'A', description: null, points: 3, parentId: null, tasks: [] }],
    sprintId: null,
    placement: null,
  };
  post.mockResolvedValue({ created: [] });
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

  await act(() => add.mutateAsync(request));

  expect(post).toHaveBeenCalledWith('/api/projects/p1/backlog/items', request);
  expect(invalidate).toHaveBeenCalledWith({ queryKey: backlogRootKey('p1') });
  expect(invalidate).toHaveBeenCalledWith({ queryKey: ['projects', 'by-key', 'SHIP'] });
});

test('useDuplicateCheck_WhenChecked_ShouldPostTheTitlesAndNotInvalidate', async () => {
  post.mockResolvedValue({ matches: [{ index: 0, items: [{ key: 'SHIP-1', title: 'A' }] }] });
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries');

  const result = await act(() => check.mutateAsync({ titles: ['A'] }));

  expect(post).toHaveBeenCalledWith('/api/projects/p1/backlog/duplicates', { titles: ['A'] });
  expect(result.matches[0].index).toBe(0);
  expect(invalidate).not.toHaveBeenCalled();
});
