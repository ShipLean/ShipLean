import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '../../../api/projects';
import { fakeConnection } from '../../../realtime/testing/fakeConnection';

const hub = fakeConnection();
vi.mock('../../../realtime/connection', async (original) => ({
  ...(await original<typeof import('../../../realtime/connection')>()),
  createProjectConnection: () => hub.connection,
}));
const gets: string[] = [];
vi.mock('../../../api/client', async (original) => ({
  ...(await original<typeof import('../../../api/client')>()),
  api: {
    get: vi.fn((url: string) => {
      gets.push(url);
      return Promise.resolve({ rows: [], sections: [], tagColours: [] });
    }),
  },
}));

const { useProjectRealtime } = await import('../../../realtime/useProjectRealtime');
const { useBacklog } = await import('../backlogApi');

const projectId = '0199a000-0000-7000-8000-000000000012';

function Screen({ level }: { level: 'features' | 'epics' }) {
  useProjectRealtime('SHIP');
  useBacklog({ projectId, level, mode: 'list', filter: '', includeCompleted: false });
  return null;
}

beforeEach(() => {
  vi.useFakeTimers();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  gets.length = 0;
});
afterEach(() => vi.useRealTimers());

for (const level of ['features', 'epics'] as const) {
  for (const type of [
    'workItemUpdated',
    'workItemCreated',
    'workItemMoved',
    'workItemRemoved',
    'boardSettingsChanged',
    'statesChanged',
  ]) {
    test(`useBacklog_When${type}FromAnotherClientOn${level}_ShouldRefetchTheLevel`, async () => {
      const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false, staleTime: Infinity } },
      });
      queryClient.setQueryData(['projects', 'by-key', 'SHIP'], { id: projectId } as ProjectDto);
      const root = createRoot(document.createElement('div'));
      act(() => {
        root.render(
          <QueryClientProvider client={queryClient}>
            <Screen level={level} />
          </QueryClientProvider>,
        );
      });
      await act(() => vi.advanceTimersByTimeAsync(0));
      const reads = () => gets.filter((u) => u.includes(`/backlogs/${level}?`)).length;
      expect(reads()).toBe(1);
      act(() =>
        hub.deliver({ type, projectId, clientId: 'someone-else', id: 'x', fields: ['title'] }),
      );
      await act(() => vi.advanceTimersByTimeAsync(0));
      expect(reads()).toBe(2);
      act(() => root.unmount());
    });
  }
}
