import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '../../api/projects';
import type { ProjectEvent } from '../../api/events';
import { fakeConnection } from '../../realtime/testing/fakeConnection';

const hub = fakeConnection();
vi.mock('../../realtime/connection', async (original) => ({
  ...(await original<typeof import('../../realtime/connection')>()),
  createProjectConnection: () => hub.connection,
}));
const reads = vi.hoisted(() => ({ count: 0 }));
vi.mock('../../api/client', async (original) => ({
  ...(await original<typeof import('../../api/client')>()),
  api: {
    get: vi.fn(async () => {
      reads.count += 1;
      return { boardId: 'board-1' };
    }),
  },
}));

const { useProjectRealtime } = await import('../../realtime/useProjectRealtime');
const { useBoard } = await import('./boardApi');
const { moveSettled, moveStarted } = await import('./moves/pendingMoves');

const projectId = '0199a000-0000-7000-8000-000000000012';
const boardId = 'board-1';

function Screen() {
  useProjectRealtime('SHIP');
  const { settingsChanges } = useBoard(projectId, boardId, {});
  return <output>{settingsChanges}</output>;
}

let root: Root;
let container: HTMLElement;
const settingsChanges = () => Number(container.textContent);
beforeEach(async () => {
  vi.useFakeTimers();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  reads.count = 0;
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  queryClient.setQueryData(['projects', 'by-key', 'SHIP'], { id: projectId } as ProjectDto);
  container = document.createElement('div');
  root = createRoot(container);
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

// Delivers one event from a teammate (no clientId) and lets the refetch it causes run.
async function deliver(event: Record<string, unknown>) {
  hub.deliver({ projectId, clientId: null, ...event });
  await act(() => vi.advanceTimersByTimeAsync(0));
}

const refetches = [
  { type: 'workItemCreated', id: 'i', key: 'SHIP-1', workItemType: 'story', parentId: null },
  { type: 'workItemUpdated', id: 'i', revision: 2, fields: ['title'], pulled: false },
  {
    type: 'workItemMoved',
    id: 'i',
    revision: 2,
    columnId: 'c',
    done: false,
    laneId: null,
    columnOrder: 'a',
    stateId: 's',
    parentId: null,
  },
  { type: 'workItemRemoved', id: 'i', parentId: null },
  { type: 'statesChanged' },
  { type: 'projectChanged' },
] satisfies Record<string, unknown>[];

test.each(refetches)('useBoard_WhenEvent$type_ShouldRefetchTheBoard', async (event) => {
  expect(reads.count).toBe(1);
  await deliver(event);
  expect(reads.count).toBe(2);
  expect(settingsChanges()).toBe(0);
});

test('useBoard_WhenAnotherBoardsSettingsChange_ShouldNeitherRefetchNorFlash', async () => {
  await deliver({ type: 'boardSettingsChanged', boardId: 'board-2', sections: ['columns'] });
  expect(reads.count).toBe(1);
  expect(settingsChanges()).toBe(0);
});

test('useBoard_WhenThisBoardsSettingsChange_ShouldRefetchAndCountTheChange', async () => {
  await deliver({ type: 'boardSettingsChanged', boardId, sections: ['columns'] });
  expect(reads.count).toBe(2);
  expect(settingsChanges()).toBe(1);
});

test('useBoard_WhenAnUnrelatedEventArrives_ShouldNotRefetch', async () => {
  await deliver({ type: 'sprintChanged', sprintId: 's1' } satisfies Partial<ProjectEvent>);
  expect(reads.count).toBe(1);
});

test('useBoard_WhenAMoveIsInFlight_ShouldNotRefetchOnATeammatesEventUntilTheLastMoveSettles', async () => {
  expect(reads.count).toBe(1);
  moveStarted(boardId);
  moveStarted(boardId);
  await deliver({
    type: 'workItemUpdated',
    id: 'i',
    revision: 2,
    fields: ['title'],
    pulled: false,
  });
  await deliver({ type: 'statesChanged' });
  expect(reads.count).toBe(1);
  expect(moveSettled(boardId)).toBe(false);
  expect(moveSettled(boardId)).toBe(true);
  await deliver({ type: 'statesChanged' });
  expect(reads.count).toBe(2);
});
