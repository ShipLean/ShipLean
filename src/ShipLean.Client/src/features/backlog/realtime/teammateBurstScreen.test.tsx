import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '../../../api/projects';
import type { BacklogRowDto } from '../../../api/views';
import { getToasts } from '../../../components/toastStore';
import { fakeConnection } from '../../../realtime/testing/fakeConnection';
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
const backlogReads = vi.fn();
vi.mock('../../../api/client', async (original) => ({
  ...(await original<typeof import('../../../api/client')>()),
  api: { get: (...args: unknown[]) => backlogReads(...args) },
}));

const { useProjectRealtime } = await import('../../../realtime/useProjectRealtime');
const { useBacklog } = await import('../backlogApi');
const { useTeammateChanges } = await import('./useTeammateChanges');

const projectId = '0199a000-0000-7000-8000-000000000012';
const remote = (type: string, extra: object) => ({
  type,
  projectId,
  clientId: 'someone-else',
  ...extra,
});
const KEYS = Array.from({ length: 100 }, (_, i) => `SHIP-${i + 1}`);

const flashes: ReadonlySet<string>[] = [];

function Screen() {
  useProjectRealtime('SHIP');
  const query = useBacklog({
    projectId,
    level: 'stories',
    mode: 'list',
    filter: '',
    includeCompleted: false,
  });
  const rows = (query.data?.rows ?? []) as BacklogRowDto[];
  flashes.push(useTeammateChanges(projectId, rows));
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

// A bulk move of `count` rows: each row publishes an update and a reorder.
function deliverMove(count: number) {
  act(() => {
    for (const key of KEYS.slice(0, count)) {
      hub.deliver(remote('workItemUpdated', { id: `id-${key}`, fields: ['sprint'] }));
      hub.deliver(remote('workItemReordered', { id: `id-${key}`, backlogOrder: 'a' }));
    }
  });
}

beforeEach(() => {
  vi.useFakeTimers();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  flashes.length = 0;
  fetchWorkItem.mockReset();
  fetchWorkItem.mockResolvedValue({ changedBy: { name: 'Priya Shah' }, sprint: null });
  backlogReads.mockReset();
  backlogReads.mockResolvedValue(backlogView(KEYS.map((key) => itemRow({ key, revision: 1 }))));
});
afterEach(() => vi.useRealTimers());

test('Burst_of_100_shows_one_toast', async () => {
  const root = await mountScreen();
  const before = toasts().length;
  deliverMove(100);
  await act(() => vi.advanceTimersByTimeAsync(2000));

  expect(toasts().slice(before)).toEqual(['100 items changed by a teammate']);
  act(() => root.unmount());
});

test('Burst_of_100_reads_no_item', async () => {
  const root = await mountScreen();
  deliverMove(100);
  await act(() => vi.advanceTimersByTimeAsync(2000));

  expect(fetchWorkItem).not.toHaveBeenCalled();
  act(() => root.unmount());
});

test('Burst_of_100_refetches_at_most_twice', async () => {
  const root = await mountScreen();
  backlogReads.mockClear();
  deliverMove(100);
  await act(() => vi.advanceTimersByTimeAsync(2000));

  expect(backlogReads.mock.calls.length).toBeGreaterThan(0);
  expect(backlogReads.mock.calls.length).toBeLessThanOrEqual(2);
  act(() => root.unmount());
});

test('Three_changes_still_toast_one_by_one', async () => {
  const root = await mountScreen();
  const before = toasts().length;
  act(() => {
    for (const key of KEYS.slice(0, 3)) {
      hub.deliver(remote('workItemUpdated', { id: `id-${key}`, fields: ['storyPoints'] }));
    }
  });
  await act(() => vi.advanceTimersByTimeAsync(2000));

  expect(toasts().slice(before)).toEqual([
    'Priya Shah changed story points on SHIP-1',
    'Priya Shah changed story points on SHIP-2',
    'Priya Shah changed story points on SHIP-3',
  ]);
  expect(fetchWorkItem).toHaveBeenCalledTimes(3);
  act(() => root.unmount());
});

test('A_burst_still_flashes_every_row', async () => {
  const root = await mountScreen();
  flashes.length = 0;
  deliverMove(100);
  await act(() => vi.advanceTimersByTimeAsync(0));

  const flashed = new Set(flashes.flatMap((set) => [...set]));
  expect(flashed.size).toBe(100);
  act(() => root.unmount());
});

test('Four_changes_100ms_apart_shows_one_toast_and_reads_no_item', async () => {
  const root = await mountScreen();
  const before = toasts().length;
  for (const key of KEYS.slice(0, 4)) {
    act(() => hub.deliver(remote('workItemUpdated', { id: `id-${key}`, fields: ['storyPoints'] })));
    await act(() => vi.advanceTimersByTimeAsync(100));
  }
  await act(() => vi.advanceTimersByTimeAsync(2000));

  expect(toasts().slice(before)).toEqual(['4 items changed by a teammate']);
  expect(fetchWorkItem).not.toHaveBeenCalled();
  act(() => root.unmount());
});

test('Three_changes_100ms_apart_toast_one_by_one', async () => {
  const root = await mountScreen();
  const before = toasts().length;
  for (const key of KEYS.slice(0, 3)) {
    act(() => hub.deliver(remote('workItemUpdated', { id: `id-${key}`, fields: ['storyPoints'] })));
    await act(() => vi.advanceTimersByTimeAsync(100));
  }
  await act(() => vi.advanceTimersByTimeAsync(2000));

  expect(toasts().slice(before)).toEqual([
    'Priya Shah changed story points on SHIP-1',
    'Priya Shah changed story points on SHIP-2',
    'Priya Shah changed story points on SHIP-3',
  ]);
  act(() => root.unmount());
});

test('Unmount_before_the_window_closes_shows_nothing', async () => {
  const root = await mountScreen();
  const before = toasts().length;
  act(() => hub.deliver(remote('workItemUpdated', { id: 'id-SHIP-1', fields: ['storyPoints'] })));
  act(() => root.unmount());
  await act(() => vi.advanceTimersByTimeAsync(2000));

  expect(toasts().slice(before)).toEqual([]);
  expect(fetchWorkItem).not.toHaveBeenCalled();
});
