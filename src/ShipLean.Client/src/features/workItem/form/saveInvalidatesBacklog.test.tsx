import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectDto } from '@shiplean/core/api/projects';
import { backlogKey } from '../../../queries/backlogKeys';
import { emptyAsks } from '../../asks/ct/fixtures';
import { story } from '../ct/fixtures';
import { workItemTestIds as ids } from '../testIds';
import { workItemKey } from '../workItemApi';

const patches = vi.hoisted(() => ({ bodies: [] as unknown[] }));
vi.mock('@shiplean/core/api/client', async (original) => ({
  ...(await original<typeof import('@shiplean/core/api/client')>()),
  api: {
    get: vi.fn(async (url: string) => {
      if (url.endsWith('/asks')) return emptyAsks(story.id);
      return url.includes('/work-items/by-key/') ? { ...story, revision: 6, title: 'Renamed' } : [];
    }),
    patch: vi.fn(async (_url: string, body: unknown) => {
      patches.bodies.push(body);
      return { id: story.id, key: story.key, revision: 6, changes: [], notices: [] };
    }),
  },
}));

const { WorkItemForm } = await import('./WorkItemForm');
const { ToastHost } = await import('../../../components/ToastHost');

const project = {
  id: story.projectId,
  key: 'SHIP',
  name: 'Shipping',
  description: '',
  role: 'admin',
  memberCount: 2,
  workingDays: [1, 2, 3, 4, 5],
  velocityWindow: 3,
  velocityOverride: null,
  nextItemNumber: 200,
} as ProjectDto;

let root: Root;
let host: HTMLElement;
let queryClient: QueryClient;
beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  patches.bodies = [];
  host = document.createElement('div');
  document.body.append(host);
  queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  root = createRoot(host);
});
afterEach(() => {
  act(() => root.unmount());
  host.remove();
});

const byId = (id: string) => host.querySelector<HTMLElement>(`[data-testid="${id}"]`);

async function renderForm() {
  await act(async () => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <ToastHost />
          <WorkItemForm
            item={story}
            project={project}
            links={null}
            tasks={null}
            discussion={null}
            side={null}
          />
        </MemoryRouter>
      </QueryClientProvider>,
    );
  });
}

async function renameAndSave() {
  const title = byId(ids.title) as HTMLInputElement;
  await act(async () => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')?.set?.call(
      title,
      'Renamed',
    );
    title.dispatchEvent(new Event('input', { bubbles: true }));
  });
  await act(async () => {
    byId(ids.save)?.click();
  });
}

test('Saving_in_the_modal_invalidates_the_backlog', async () => {
  const rows = backlogKey(story.projectId, 'stories', 'list', '', false);
  queryClient.setQueryData(rows, { rows: [] });
  queryClient.setQueryData(workItemKey(story.projectId, story.key), story);
  await renderForm();

  await renameAndSave();

  expect(patches.bodies).toHaveLength(1);
  expect(queryClient.getQueryState(rows)?.isInvalidated).toBe(true);
});

test('Saving_in_the_modal_leaves_another_projects_backlog_alone', async () => {
  const other = backlogKey('another-project', 'stories', 'list', '', false);
  queryClient.setQueryData(other, { rows: [] });
  queryClient.setQueryData(workItemKey(story.projectId, story.key), story);
  await renderForm();

  await renameAndSave();

  expect(patches.bodies).toHaveLength(1);
  expect(queryClient.getQueryState(other)?.isInvalidated).toBe(false);
});
