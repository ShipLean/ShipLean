import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, test, vi } from 'vitest';

const handlers: [string, () => void][] = [];
vi.mock('../../../realtime', () => ({
  registerInvalidation: (type: string, handler: () => void) => {
    handlers.push([type, handler]);
    return () => {};
  },
}));

const { useInstanceProjects, instanceProjectsKey } = await import('./instanceApi');

function Probe() {
  useInstanceProjects();
  return null;
}

test('useInstanceProjects_WhenProjectChangedByTeammate_ShouldReadProjectsAgain', () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  const spy = vi.spyOn(client, 'invalidateQueries');
  const root = createRoot(document.createElement('div'));
  act(() =>
    root.render(
      <QueryClientProvider client={client}>
        <Probe />
      </QueryClientProvider>,
    ),
  );

  handlers.find(([type]) => type === 'projectChanged')?.[1]();

  expect(spy).toHaveBeenCalledWith({ queryKey: instanceProjectsKey });
});
