import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { BoardViewDto } from '@shiplean/core/api/views';
import type { UserRef } from '@shiplean/core/api/refs';
import { card, cell, lane, storiesBoard } from '../../ct/fixtures';
import { boardRootKey } from '../../boardApi';

const patch = vi.hoisted(() => vi.fn());
vi.mock('@shiplean/core/api/client', async (original) => ({
  ...(await original<typeof import('@shiplean/core/api/client')>()),
  api: { patch },
}));

const { ApiRequestError } = await import('@shiplean/core/api/client');
const { useAssignCard } = await import('./useAssignCard');

const ann: UserRef = {
  id: 'u-ann',
  name: 'Ann',
  initials: 'A',
  avatarColour: '#112233',
  photoUrl: null,
};
const bob: UserRef = {
  id: 'u-bob',
  name: 'Bob',
  initials: 'B',
  avatarColour: '#223344',
  photoUrl: null,
};

let root: Root;
let container: HTMLElement;
let queryClient: QueryClient;
const assigners: ReturnType<typeof useAssignCard>[] = [];
const key = boardRootKey('p1', 'b1');

function Probe() {
  const assign = useAssignCard('p1', 'b1');
  useEffect(() => {
    assigners[0] = assign;
  });
  return null;
}

function seed(): BoardViewDto {
  const view = storiesBoard({
    lanes: [lane({ id: 'l', cells: [cell('col-new', [card({ key: 'SHIP-1', assignee: ann })])] })],
  });
  queryClient.setQueryData([...key, 'seed'], view);
  return view;
}
const assigneeNow = () =>
  queryClient.getQueryData<BoardViewDto>([...key, 'seed'])!.lanes[0]!.cells[0]!.cards[0]!;

beforeEach(() => {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  patch.mockReset();
  queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  container = document.createElement('div');
  document.body.append(container);
  root = createRoot(container);
  act(() => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <Probe />
      </QueryClientProvider>,
    );
  });
});
afterEach(() => {
  act(() => root.unmount());
  container.remove();
});

test('useAssignCard_WhenConflict_ShouldNotApplyLocally', async () => {
  const view = seed();
  const target = view.lanes[0]!.cells[0]!.cards[0]!;
  patch.mockRejectedValue(
    new ApiRequestError('Changed by someone else.', 409, 'conflict', {
      current: {},
      theirChanges: [],
    }),
  );
  await act(async () => assigners[0]!(target, bob));
  expect(patch).toHaveBeenCalledTimes(1);
  expect(assigneeNow().assignee?.id).toBe('u-ann');
  expect(assigneeNow().revision).toBe(target.revision);
});

test('useAssignCard_WhenAccepted_ShouldSetAssigneeAndRevision', async () => {
  const view = seed();
  const target = view.lanes[0]!.cells[0]!.cards[0]!;
  patch.mockResolvedValue({ id: target.id, key: 'SHIP-1', revision: 7, changes: [], notices: [] });
  await act(async () => assigners[0]!(target, null));
  expect(patch).toHaveBeenCalledWith(`/api/work-items/${target.id}`, {
    expectedRevision: target.revision,
    changes: expect.objectContaining({ assignee: { value: null }, state: null, title: null }),
  });
  expect(assigneeNow().assignee).toBeNull();
  expect(assigneeNow().revision).toBe(7);
});
