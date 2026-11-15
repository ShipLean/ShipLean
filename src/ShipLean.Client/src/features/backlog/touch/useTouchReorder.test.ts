import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, createElement, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { ApiRequestError } from '@shiplean/core/api/client';
import type { BacklogViewDto } from '@shiplean/core/api/views';
import { backlogView, itemRow } from '../ct/fixtures';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const post = vi.hoisted(() => vi.fn(async () => ({})));
vi.mock('@shiplean/core/api/client', async (original) => ({
  ...(await original<typeof import('@shiplean/core/api/client')>()),
  api: { post },
}));

const { useTouchReorder } = await import('./useTouchReorder');

const KEY = ['backlog', 'p1', 'test'] as const;
type Reorder = ReturnType<typeof useTouchReorder>;
const seen: { reorder: Reorder | null } = { reorder: null };

function Probe() {
  const reorder = useTouchReorder('p1', KEY);
  useEffect(() => {
    seen.reorder = reorder;
  });
  return null;
}

let root: Root;
let client: QueryClient;
const view = () =>
  backlogView([
    itemRow({ key: 'SHIP-1', revision: 4 }),
    itemRow({ key: 'SHIP-2', revision: 7 }),
    itemRow({ key: 'SHIP-3' }),
  ]);

beforeEach(async () => {
  post.mockClear();
  client = new QueryClient();
  client.setQueryData<BacklogViewDto>(KEY, view());
  root = createRoot(document.createElement('div'));
  await act(async () => {
    root.render(createElement(QueryClientProvider, { client }, createElement(Probe)));
  });
});
afterEach(() => act(() => root.unmount()));

const dropOn = (rowId: string, targetId: string | null) =>
  act(async () => seen.reorder?.drop(rowId, targetId));
const order = () => client.getQueryData<BacklogViewDto>(KEY)?.rows.map((row) => row.key);

test('useTouchReorder_WhenDroppedOnRow_ShouldSendBeforeThatRow', async () => {
  await dropOn('id-SHIP-1', 'id-SHIP-3');

  expect(post).toHaveBeenCalledTimes(1);
  expect(post).toHaveBeenCalledWith('/api/work-items/id-SHIP-1/reorder', {
    expectedRevision: 4,
    beforeItemId: 'id-SHIP-3',
  });
  expect(order()).toEqual(['SHIP-2', 'SHIP-1', 'SHIP-3']);
});

test('useTouchReorder_WhenDroppedOnItself_ShouldSendNothing', async () => {
  await dropOn('id-SHIP-2', 'id-SHIP-2');

  expect(post).not.toHaveBeenCalled();
  expect(order()).toEqual(['SHIP-1', 'SHIP-2', 'SHIP-3']);
});

test('useTouchReorder_WhenDroppedOutsideARow_ShouldSendNothing', async () => {
  await dropOn('id-SHIP-2', null);

  expect(post).not.toHaveBeenCalled();
  expect(order()).toEqual(['SHIP-1', 'SHIP-2', 'SHIP-3']);
});

test('useTouchReorder_ShouldReturnTheUseReorderItSends', async () => {
  post.mockRejectedValueOnce(
    new ApiRequestError('Changed.', 409, 'conflict', {
      error: 'Changed.',
      code: 'conflict',
      current: { revision: 9 },
      theirChanges: [{ changedBy: { name: 'Tom' } }],
    }),
  );

  await dropOn('id-SHIP-2', 'id-SHIP-1');

  // The write went through the hook's own useReorder, so its conflict is the one the returned values read.
  const returned = seen.reorder;
  expect(returned?.reorder.conflict?.itemKey).toBe('SHIP-2');
  expect(returned?.reorder.conflict?.currentRevision).toBe(9);
  expect(returned?.conflict).toBe(returned?.reorder.conflict);
  expect(returned?.useMine).toBe(returned?.reorder.useMine);
  expect(returned?.keepTheirs).toBe(returned?.reorder.keepTheirs);
});
