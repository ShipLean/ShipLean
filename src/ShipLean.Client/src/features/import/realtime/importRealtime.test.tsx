import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { createRoot } from 'react-dom/client';
import { beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '../../../api/projects';
import { importJobsKey, jiraProjectsKey } from '../importApi';
import { projectByKeyKey } from '../../../queries/shared';

const handlers: [string, () => void][] = [];
vi.mock('../../../realtime', () => ({
  registerInvalidation: (type: string, handler: () => void) => {
    handlers.push([type, handler]);
    return () => {};
  },
}));

const { ImportJobsList } = await import('../integrations/ImportJobsList');
const { ImportStepper } = await import('../stepper/ImportStepper');

const project = { id: 'p1', key: 'SHIP', role: 'admin', nextItemNumber: 1 } as ProjectDto;

function mountWith(node: (client: QueryClient) => React.ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  const spy = vi.spyOn(client, 'invalidateQueries');
  const root = createRoot(document.createElement('div'));
  act(() =>
    root.render(
      <QueryClientProvider client={client}>
        <MemoryRouter>{node(client)}</MemoryRouter>
      </QueryClientProvider>,
    ),
  );
  return spy;
}

beforeEach(() => {
  handlers.length = 0;
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  vi.stubGlobal('fetch', () => new Promise(() => {}));
});

const fire = (type: string) =>
  act(() => handlers.filter(([t]) => t === type).forEach(([, h]) => h()));

test('importJobs_WhenProgressArrives_ShouldRefetchJobsAndJiraProjects', () => {
  const spy = mountWith(() => <ImportJobsList projectId="p1" />);
  fire('importProgress');
  const keys = spy.mock.calls.map(([filters]) => filters?.queryKey);
  expect(keys).toContainEqual(importJobsKey('p1'));
  expect(keys).toContainEqual(jiraProjectsKey('p1'));
});

test('importStepper_WhenAWorkItemIsCreatedElsewhere_ShouldRefetchTheProject', () => {
  const spy = mountWith(() => <ImportStepper project={project} />);
  fire('workItemCreated');
  expect(spy.mock.calls.map(([filters]) => filters?.queryKey)).toContainEqual(
    projectByKeyKey('SHIP'),
  );
});
