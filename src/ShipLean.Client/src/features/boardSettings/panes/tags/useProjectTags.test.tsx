import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '../../../../api/projects';
import { fakeConnection } from '../../../../realtime/testing/fakeConnection';

const hub = fakeConnection();
vi.mock('../../../../realtime/connection', async (original) => ({
  ...(await original<typeof import('../../../../realtime/connection')>()),
  createProjectConnection: () => hub.connection,
}));
const get = vi.fn();
vi.mock('../../../../api/client', async (original) => {
  const actual = await original<typeof import('../../../../api/client')>();
  return { ...actual, api: { ...actual.api, get: (...args: unknown[]) => get(...args) } };
});

const { useProjectRealtime } = await import('../../../../realtime/useProjectRealtime');
const { useProjectTags } = await import('./useProjectTags');

const projectId = '0199a000-0000-7000-8000-000000000012';

function Screen() {
  useProjectRealtime('SHIP');
  useProjectTags(projectId);
  return null;
}

beforeEach(() => {
  vi.useFakeTimers();
  get.mockReset();
  get.mockImplementation(async (url: string) =>
    url.endsWith('/tags') ? ['a'] : ({ id: projectId } as ProjectDto),
  );
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
});

afterEach(() => vi.useRealTimers());

async function mounted() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  queryClient.setQueryData(['projects', 'by-key', 'SHIP'], { id: projectId } as ProjectDto);
  const root = createRoot(document.createElement('div'));
  await act(async () => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <Screen />
      </QueryClientProvider>,
    );
  });
  await act(() => vi.advanceTimersByTimeAsync(0));
  return root;
}

const tagReads = () => get.mock.calls.filter(([url]) => String(url).endsWith('/tags')).length;

const remote = (type: string) => ({ type, projectId, clientId: 'someone-else' });

test.each(['workItemCreated', 'workItemUpdated', 'workItemRemoved'])(
  'useProjectTags_When%sArrives_ShouldReadTheTagsAgain',
  async (type) => {
    const root = await mounted();
    const before = tagReads();
    expect(get).toHaveBeenCalledWith(`/api/projects/${projectId}/tags`);

    act(() => hub.deliver(remote(type)));
    await act(() => vi.advanceTimersByTimeAsync(0));

    expect(tagReads()).toBeGreaterThan(before);
    act(() => root.unmount());
  },
);

test('useProjectTags_WhenUnrelatedEventArrives_ShouldNotReadAgain', async () => {
  const root = await mounted();
  const before = tagReads();

  act(() => hub.deliver(remote('sprintChanged')));
  await act(() => vi.advanceTimersByTimeAsync(0));

  expect(tagReads()).toBe(before);
  act(() => root.unmount());
});
