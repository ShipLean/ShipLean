import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { beforeEach, expect, test, vi } from 'vitest';
import type { RouteId } from '../routes';
import { projectDto } from './pagesFixtures';

const useScreenPresence = vi.fn<(routeId: RouteId) => void>();
const useScreenView = vi.fn<(routeId: RouteId, projectId: string | null, mode?: string) => void>();
vi.mock('../realtime', () => ({ useScreenPresence }));
vi.mock('../realtime/useScreenView', () => ({ useScreenView }));

const { ProjectScreen } = await import('./ProjectScreen');

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  useScreenPresence.mockClear();
  useScreenView.mockClear();
});

function renderScreen(queryClient: QueryClient, routeId: RouteId = 'capacity', at = '/') {
  const router = createMemoryRouter(
    [
      {
        path: '*',
        element: (
          <ProjectScreen routeId={routeId} testId="x">
            {() => null}
          </ProjectScreen>
        ),
      },
    ],
    { initialEntries: [at] },
  );
  const root = createRoot(document.createElement('div'));
  act(() => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>,
    );
  });
  return root;
}

test('projectScreen_WhenProjectResolved_ShouldReportTheScreenViewWithItsProjectId', () => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const project = projectDto('SHIP');
  queryClient.setQueryData(['projects', 'by-key', ''], project);
  const root = renderScreen(queryClient);
  expect(useScreenView).toHaveBeenCalledTimes(1);
  expect(useScreenView).toHaveBeenCalledWith('capacity', project.id);
  act(() => root.unmount());
});

test('projectScreen_WhenProjectNotResolved_ShouldNotReportAScreenView', () => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  queryClient.setQueryDefaults(['projects', 'by-key', ''], {
    queryFn: () => new Promise(() => undefined),
  });
  const root = renderScreen(queryClient);
  expect(useScreenView).not.toHaveBeenCalled();
  act(() => root.unmount());
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

test('projectScreen_WhenFeaturesBacklogInTree_ShouldReportTheScreenViewWithTheMode', () => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const project = projectDto('SHIP');
  // The route is '*', so the screen reads the project under an empty key.
  queryClient.setQueryData(['projects', 'by-key', ''], project);
  const root = renderScreen(queryClient, 'featuresBacklog', '/p/SHIP/backlogs/features?mode=tree');
  expect(useScreenView).toHaveBeenCalledWith('featuresBacklog', project.id, 'tree');
  act(() => root.unmount());
});
