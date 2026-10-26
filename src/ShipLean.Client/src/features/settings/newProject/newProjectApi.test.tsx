import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, test, vi } from 'vitest';
import type { ProjectCreate } from '../../../api/projects';
import { projectsKey } from '../../../queries/shared';

const handlers: [string, () => void][] = [];
vi.mock('../../../realtime', () => ({
  registerInvalidation: (type: string, handler: () => void) => {
    handlers.push([type, handler]);
    return () => {};
  },
}));
vi.mock('../../../api/client', () => ({
  api: { post: vi.fn(() => Promise.resolve({ id: 'p-new', key: 'CSH' })) },
}));

const { useCopySources, useCreateProject } = await import('./newProjectApi');

const create: ProjectCreate = {
  name: 'Customer Support Hub',
  key: 'CSH',
  copyFromProjectId: null,
  memberEmails: [],
  workingDays: [1],
  firstSprint: null,
};

function mountProbe(probe: () => unknown) {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  const spy = vi.spyOn(client, 'invalidateQueries');
  const Probe = () => {
    probe();
    return null;
  };
  const root = createRoot(document.createElement('div'));
  act(() =>
    root.render(
      <QueryClientProvider client={client}>
        <Probe />
      </QueryClientProvider>,
    ),
  );
  return spy;
}

test('useCopySources_WhenProjectChangedByTeammate_ShouldReadProjectsAgain', () => {
  const spy = mountProbe(useCopySources);

  handlers.find(([type]) => type === 'projectChanged')?.[1]();

  expect(spy).toHaveBeenCalledWith({ queryKey: projectsKey });
});

test('useCreateProject_WhenCreated_ShouldDropTheProjectListAndOpenTheNewOne', async () => {
  const opened = vi.fn();
  let mutate: (c: ProjectCreate) => void = () => {};
  const spy = mountProbe(() => {
    mutate = useCreateProject(opened).mutate;
  });

  await act(async () => {
    mutate(create);
    await vi.waitFor(() => expect(opened).toHaveBeenCalledWith({ id: 'p-new', key: 'CSH' }));
  });

  expect(spy).toHaveBeenCalledWith({ queryKey: projectsKey });
  expect(spy).toHaveBeenCalledWith({ queryKey: ['me'] });
});
