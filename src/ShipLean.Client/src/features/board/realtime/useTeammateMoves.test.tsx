import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '../../../api/projects';
import { getToasts } from '../../../components/toastStore';
import { fakeConnection } from '../../../realtime/testing/fakeConnection';
import { storiesBoard } from '../ct/fixtures';

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

const { useProjectRealtime } = await import('../../../realtime/useProjectRealtime');
const { useTeammateMoves } = await import('./useTeammateMoves');

const projectId = 'p1';
const view = storiesBoard();
const flashes: ReadonlySet<string>[] = [];

function Screen() {
  useProjectRealtime('SHIP');
  flashes.push(useTeammateMoves(projectId, view));
  return null;
}

let root: Root;
beforeEach(async () => {
  vi.useFakeTimers();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  flashes.length = 0;
  fetchWorkItem.mockReset();
  fetchWorkItem.mockResolvedValue({
    changedBy: { name: 'Tom Okafor' },
    state: { name: 'Active' },
  });
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
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

const toasts = () => getToasts().map((t) => t.message);
const moved = (id: string, place: { columnId: string; done: boolean; laneId: string | null }) => ({
  type: 'workItemMoved',
  projectId,
  clientId: 'someone-else',
  id,
  revision: 2,
  columnOrder: 'a',
  stateId: 's-act',
  parentId: null,
  ...place,
});

test('teammateMoves_WhenACardMovesToAnotherColumn_ShouldFlashItAndNameWhoMovedIt', async () => {
  const before = toasts().length;
  act(() =>
    hub.deliver(
      moved('id-SHIP-160', { columnId: 'col-prog', done: false, laneId: 'lane-default' }),
    ),
  );
  await act(() => vi.advanceTimersByTimeAsync(0));

  expect(toasts().slice(before)).toEqual(['Tom Okafor moved SHIP-160 to Active']);
  expect(flashes.at(-1)?.has('id-SHIP-160')).toBe(true);
});

test('teammateMoves_WhenACardIsOnlyReordered_ShouldFlashItAndNameNobody', async () => {
  const before = toasts().length;
  act(() =>
    hub.deliver(moved('id-SHIP-160', { columnId: 'col-new', done: false, laneId: 'lane-default' })),
  );
  await act(() => vi.advanceTimersByTimeAsync(0));

  expect(toasts().slice(before)).toEqual(['SHIP-160 was reordered']);
  expect(fetchWorkItem).not.toHaveBeenCalled();
  expect(flashes.at(-1)?.has('id-SHIP-160')).toBe(true);
});

test('teammateMoves_WhenTheCardIsNotOnTheBoard_ShouldSayNothing', async () => {
  const before = toasts().length;
  act(() =>
    hub.deliver(
      moved('id-SHIP-999', { columnId: 'col-prog', done: false, laneId: 'lane-default' }),
    ),
  );
  await act(() => vi.advanceTimersByTimeAsync(0));

  expect(toasts().slice(before)).toEqual([]);
  expect(flashes.at(-1)?.size ?? 0).toBe(0);
});

test('teammateMoves_WhenTheFlashHasRun_ShouldStopFlashing', async () => {
  act(() =>
    hub.deliver(
      moved('id-SHIP-160', { columnId: 'col-prog', done: false, laneId: 'lane-default' }),
    ),
  );
  await act(() => vi.advanceTimersByTimeAsync(3000));

  expect(flashes.at(-1)?.size).toBe(0);
});
