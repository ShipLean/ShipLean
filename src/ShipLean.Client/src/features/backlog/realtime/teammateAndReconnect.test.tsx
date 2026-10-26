import { QueryClient, QueryClientProvider, useQuery, useQueryClient } from '@tanstack/react-query';
import { act, useEffect } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '../../../api/projects';
import type { BacklogRowDto, BacklogViewDto } from '../../../api/views';
import { getToasts } from '../../../components/toastStore';
import { fakeConnection } from '../../../realtime/testing/fakeConnection';
import { backlogKey } from '../backlogApi';
import { backlogView, itemRow } from '../ct/fixtures';

const hub = fakeConnection();
vi.mock('../../../realtime/connection', async (original) => ({
  ...(await original<typeof import('../../../realtime/connection')>()),
  createProjectConnection: () => hub.connection,
}));
const fetchWorkItem = vi.fn();
vi.mock('../../workItem/workItemApi', async (original) => ({
  ...(await original<typeof import('../../workItem/workItemApi')>()),
  fetchWorkItem: (...args: unknown[]) => fetchWorkItem(...args),
}));

const { registerInvalidation } = await import('../../../realtime');
const { useProjectRealtime } = await import('../../../realtime/useProjectRealtime');
const { useReconnectSummary } = await import('./useReconnectSummary');
const { useTeammateChanges } = await import('./useTeammateChanges');

const projectId = '0199a000-0000-7000-8000-000000000012';
const key = backlogKey(projectId, 'stories', 'list', '', false);
const remote = (type: string, extra: object) => ({
  type,
  projectId,
  clientId: 'someone-else',
  ...extra,
});

let served: BacklogViewDto;
let failReads = false;
const flashes: ReadonlySet<string>[] = [];

function Screen() {
  useProjectRealtime('SHIP');
  const queryClient = useQueryClient();
  useEffect(
    () =>
      registerInvalidation('workItemCreated', () => {
        void queryClient.invalidateQueries({ queryKey: key });
      }),
    [queryClient],
  );
  const query = useQuery({
    queryKey: key,
    queryFn: () => {
      if (failReads) return Promise.reject(new Error('offline'));
      return Promise.resolve(served);
    },
  });
  const rows = (query.data?.rows ?? []) as BacklogRowDto[];
  flashes.push(useTeammateChanges(projectId, rows));
  useReconnectSummary(key);
  return null;
}

async function mountScreen() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  queryClient.setQueryData(['projects', 'by-key', 'SHIP'], { id: projectId } as ProjectDto);
  const root = createRoot(document.createElement('div'));
  act(() => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <Screen />
      </QueryClientProvider>,
    );
  });
  await act(() => vi.advanceTimersByTimeAsync(0));
  return root;
}

const toasts = () => getToasts().map((t) => t.message);

beforeEach(() => {
  vi.useFakeTimers();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  served = backlogView([
    itemRow({ key: 'SHIP-1', revision: 1 }),
    itemRow({ key: 'SHIP-2', revision: 1 }),
  ]);
  failReads = false;
  flashes.length = 0;
  fetchWorkItem.mockReset();
  fetchWorkItem.mockResolvedValue({ changedBy: { name: 'Priya Shah' } });
});
afterEach(() => vi.useRealTimers());

test('teammateChanges_WhenReordered_ShouldFlashAndNameNobody', async () => {
  const root = await mountScreen();
  const before = toasts().length;
  act(() => hub.deliver(remote('workItemReordered', { id: 'id-SHIP-2', backlogOrder: 'a' })));
  await act(() => vi.advanceTimersByTimeAsync(0));

  expect(toasts().slice(before)).toEqual(['SHIP-2 moved in the backlog']);
  expect(fetchWorkItem).not.toHaveBeenCalled();
  expect(flashes.at(-1)?.has('id-SHIP-2')).toBe(true);
  act(() => root.unmount());
});

test('teammateChanges_WhenUpdated_ShouldNameTheLastEditor', async () => {
  const root = await mountScreen();
  const before = toasts().length;
  act(() => hub.deliver(remote('workItemUpdated', { id: 'id-SHIP-1', fields: ['storyPoints'] })));
  await act(() => vi.advanceTimersByTimeAsync(0));

  expect(toasts().slice(before)).toEqual(['Priya Shah changed story points on SHIP-1']);
  expect(flashes.at(-1)?.has('id-SHIP-1')).toBe(true);
  act(() => root.unmount());
});

test('teammateChanges_WhenSprintChangedThenReordered_ShouldToastTheMoveOnce', async () => {
  fetchWorkItem.mockResolvedValue({
    changedBy: { name: 'Tom Okafor' },
    sprint: { name: 'Sprint 25' },
  });
  const root = await mountScreen();
  const before = toasts().length;
  act(() => hub.deliver(remote('workItemUpdated', { id: 'id-SHIP-1', fields: ['sprint'] })));
  act(() => hub.deliver(remote('workItemReordered', { id: 'id-SHIP-1', backlogOrder: 'a' })));
  await act(() => vi.advanceTimersByTimeAsync(0));

  expect(toasts().slice(before)).toEqual(['Tom Okafor moved SHIP-1 to Sprint 25']);
  expect(flashes.at(-1)?.has('id-SHIP-1')).toBe(true);
  act(() => root.unmount());
});

test('teammateChanges_WhenReorderedLongAfterASprintMove_ShouldToastTheReorder', async () => {
  fetchWorkItem.mockResolvedValue({ changedBy: { name: 'Tom Okafor' }, sprint: null });
  const root = await mountScreen();
  act(() => hub.deliver(remote('workItemUpdated', { id: 'id-SHIP-1', fields: ['sprint'] })));
  await act(() => vi.advanceTimersByTimeAsync(10_000));
  const before = toasts().length;
  act(() => hub.deliver(remote('workItemReordered', { id: 'id-SHIP-1', backlogOrder: 'b' })));
  await act(() => vi.advanceTimersByTimeAsync(0));

  expect(toasts().slice(before)).toEqual(['SHIP-1 moved in the backlog']);
  act(() => root.unmount());
});

test('reconnectSummary_WhenDroppedAndBack_ShouldRefetchAndSayHowManyChanged', async () => {
  const root = await mountScreen();
  const before = toasts().length;
  served = backlogView([
    itemRow({ key: 'SHIP-2', revision: 1 }),
    itemRow({ key: 'SHIP-1', revision: 1 }),
    itemRow({ key: 'SHIP-3', revision: 1 }),
  ]);

  await act(() => vi.advanceTimersByTimeAsync(1000));
  act(() => hub.drop());
  await act(() => vi.advanceTimersByTimeAsync(60_000));

  expect(toasts().slice(before)).toEqual(['Back online. 2 items changed while you were away.']);
  act(() => root.unmount());
});

test('reconnectSummary_WhenOnlyTheOrderChanged_ShouldCountTheReorder', async () => {
  const root = await mountScreen();
  const before = toasts().length;
  served = backlogView([
    itemRow({ key: 'SHIP-2', revision: 1 }),
    itemRow({ key: 'SHIP-1', revision: 1 }),
  ]);

  await act(() => vi.advanceTimersByTimeAsync(1000));
  act(() => hub.drop());
  await act(() => vi.advanceTimersByTimeAsync(60_000));

  expect(toasts().slice(before)).toEqual(['Back online. 1 item changed while you were away.']);
  act(() => root.unmount());
});

test('reconnectSummary_WhenTheRefetchFails_ShouldSayNothingAndNotSummariseLaterReads', async () => {
  const root = await mountScreen();
  const before = toasts().length;
  failReads = true;

  await act(() => vi.advanceTimersByTimeAsync(1000));
  act(() => hub.drop());
  await act(() => vi.advanceTimersByTimeAsync(60_000));
  expect(toasts().slice(before)).toEqual([]);

  // A later read that works (not after a reconnect) is not a summary.
  failReads = false;
  served = backlogView([itemRow({ key: 'SHIP-9', revision: 1 })]);
  act(() => hub.deliver(remote('workItemCreated', { id: 'id-SHIP-9' })));
  await act(() => vi.advanceTimersByTimeAsync(0));
  expect(toasts().slice(before)).toEqual([]);
  act(() => root.unmount());
});
