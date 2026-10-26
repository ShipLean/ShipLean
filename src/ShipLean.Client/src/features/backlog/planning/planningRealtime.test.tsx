import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '../../../api/projects';
import { fakeConnection } from '../../../realtime/testing/fakeConnection';
import { planningView } from '../ct/fixtures';

const hub = fakeConnection();
vi.mock('../../../realtime/connection', async (original) => ({
  ...(await original<typeof import('../../../realtime/connection')>()),
  createProjectConnection: () => hub.connection,
}));

const { useProjectRealtime } = await import('../../../realtime/useProjectRealtime');
const { useBacklog } = await import('../backlogApi');

const projectId = '0199a000-0000-7000-8000-000000000012';
const reads: string[] = [];

function Screen() {
  useProjectRealtime('SHIP');
  useBacklog({
    projectId,
    level: 'stories',
    mode: 'planning',
    filter: '',
    includeCompleted: false,
  });
  return null;
}

beforeEach(() => {
  vi.useFakeTimers();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  reads.length = 0;
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string) => {
      reads.push(url);
      return Promise.resolve(
        new Response(JSON.stringify(planningView()), {
          status: 200,
          headers: { 'content-type': 'application/json' },
        }),
      );
    }),
  );
});
afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

// Capacity entered, a sprint created, the velocity override or the forecast changed, or an item moved by a teammate:
// the planning sections refetch.
test.each(['sprintChanged', 'projectChanged', 'workItemMoved'])(
  'planningBacklog_When%sArrives_ShouldRefetch',
  async (type) => {
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
    expect(reads).toHaveLength(1);
    expect(reads[0]).toContain('mode=planning');

    act(() => hub.deliver({ type, projectId, clientId: 'someone-else', sprintId: 'sp24' }));
    await act(() => vi.advanceTimersByTimeAsync(0));

    expect(reads).toHaveLength(2);
    act(() => root.unmount());
  },
);
