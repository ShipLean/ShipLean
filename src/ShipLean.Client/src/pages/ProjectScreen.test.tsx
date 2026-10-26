import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { beforeEach, expect, test, vi } from 'vitest';
import type { RouteId } from '../routes';
import { projectDto } from './pagesFixtures';

const useScreenPresence = vi.fn<(routeId: RouteId) => void>();
vi.mock('../realtime', () => ({ useScreenPresence }));

const { ProjectScreen } = await import('./ProjectScreen');

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  useScreenPresence.mockClear();
});

test('projectScreen_WhenRendered_ShouldReportPresenceUnderItsRouteId', () => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  queryClient.setQueryData(['projects', 'by-key', ''], projectDto('SHIP'));
  // The screen mounts the app's router blocker, which needs a data router.
  const router = createMemoryRouter([
    {
      path: '*',
      element: (
        <ProjectScreen routeId="capacity" testId="x">
          {() => null}
        </ProjectScreen>
      ),
    },
  ]);
  const root = createRoot(document.createElement('div'));
  act(() => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>,
    );
  });
  expect(useScreenPresence).toHaveBeenCalledWith('capacity');
  act(() => root.unmount());
});
