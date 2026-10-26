import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '../api/projects';
import type { RouteId } from '../routes';
import { fakeConnection } from './testing/fakeConnection';

const hub = fakeConnection();
vi.mock('./connection', async (original) => ({
  ...(await original<typeof import('./connection')>()),
  createProjectConnection: () => hub.connection,
}));

const { useProjectRealtime } = await import('./useProjectRealtime');
const { useScreenPresence } = await import('./useScreenPresence');

const projectId = '0199a000-0000-7000-8000-000000000012';

function Screen({ routeId }: { routeId: RouteId }) {
  useScreenPresence(routeId);
  useProjectRealtime('SHIP');
  return null;
}

function render(routeId: RouteId) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  queryClient.setQueryData(['projects', 'by-key', 'SHIP'], { id: projectId } as ProjectDto);
  const root = createRoot(document.createElement('div'));
  act(() => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <Screen routeId={routeId} />
      </QueryClientProvider>,
    );
  });
  return root;
}

beforeEach(() => {
  vi.useFakeTimers();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
});
afterEach(() => vi.useRealTimers());

test('screenPresence_WhenInstallLevel_ShouldNotJoin', async () => {
  const profile = render('profile');
  await act(() => vi.advanceTimersByTimeAsync(0));
  expect(hub.calls).toEqual([`JoinProject ${projectId}`]);

  act(() => profile.unmount());
  const backlog = render('storiesBacklog');
  await act(() => vi.advanceTimersByTimeAsync(0));
  expect(hub.calls).toContain(`JoinScreen ${projectId} storiesBacklog`);
  act(() => backlog.unmount());
});
