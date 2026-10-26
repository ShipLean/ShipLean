import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, expect, test, vi } from 'vitest';
import { meKey, preferenceKey, projectsKey } from '../../../../queries/shared';
import { settingsTestIds as ids } from '../../testIds';
import { MyProjectsPanel } from './MyProjectsPanel';
import { PasswordPanel } from './PasswordPanel';
import { SessionsPanel } from './SessionsPanel';

const CURRENT = 'OLD-SECRET-PASSWORD';
const NEXT = 'NEW-SECRET-PASSWORD';

const me = { id: 'me', email: 'priya@example.test', theme: 'os', density: 'compact' };
const projects = [{ id: 'p1', key: 'SHIP', name: 'Ship', role: 'member', memberCount: 2 }];

const reply = (status: number, body?: unknown) =>
  Promise.resolve(
    new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'content-type': 'application/json' },
    }),
  );

function stubFetch(passwordStatus = 200) {
  const sent: string[] = [];
  vi.stubGlobal('fetch', (url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    const path = new URL(url, 'http://localhost').pathname;
    sent.push(`${method} ${path}`);
    if (path === '/api/auth/password') {
      return passwordStatus === 200
        ? reply(200, me)
        : reply(passwordStatus, { code: 'validation', error: 'The current password is wrong.' });
    }
    if (method === 'GET') return reply(200, path === '/api/me' ? me : projects);
    return reply(204);
  });
  return sent;
}

const mounted: (() => void)[] = [];
afterEach(() => {
  mounted.splice(0).forEach((unmount) => unmount());
  document.body.replaceChildren();
  vi.unstubAllGlobals();
});

function mount(node: React.ReactNode, client = new QueryClient()) {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const container = document.body.appendChild(document.createElement('div'));
  const root = createRoot(container);
  act(() =>
    root.render(
      <QueryClientProvider client={client}>
        <MemoryRouter>{node}</MemoryRouter>
      </QueryClientProvider>,
    ),
  );
  const unmount = () => act(() => root.unmount());
  mounted.push(unmount);
  return { client, unmount };
}

const byId = <T extends HTMLElement>(id: string) =>
  document.querySelector<T>(`[data-testid="${id}"]`)!;

function type(id: string, value: string) {
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;
  act(() => {
    setter.call(byId(id), value);
    byId(id).dispatchEvent(new Event('input', { bubbles: true }));
  });
}

const click = (id: string) =>
  act(async () => {
    byId(id).click();
  });

async function changePassword(status: number) {
  stubFetch(status);
  const view = mount(<PasswordPanel />);
  type(ids.passwordCurrent, CURRENT);
  type(ids.passwordNew, NEXT);
  type(ids.passwordConfirm, NEXT);
  await click(ids.passwordSubmit);
  return view;
}

const cachedMutations = (client: QueryClient) => client.getMutationCache().getAll().length;

test('Password_WhenChanged_ShouldLeaveNoMutationHoldingEitherPassword', async () => {
  const { client, unmount } = await changePassword(200);

  await vi.waitFor(() => expect(cachedMutations(client)).toBe(0));
  unmount();
  expect(cachedMutations(client)).toBe(0);
});

test('Password_WhenRefused_ShouldLeaveNoMutationHoldingEitherPassword', async () => {
  const { client, unmount } = await changePassword(400);

  await vi.waitFor(() => expect(byId(ids.passwordError).textContent).toContain('wrong'));
  await vi.waitFor(() => expect(cachedMutations(client)).toBe(0));
  expect(byId<HTMLInputElement>(ids.passwordCurrent).value).toBe('');
  expect(byId<HTMLInputElement>(ids.passwordNew).value).toBe(NEXT);
  unmount();
  expect(cachedMutations(client)).toBe(0);
});

test('Sessions_WhenViewsReset_ShouldRefreshSavedViewQueries', async () => {
  stubFetch();
  const client = new QueryClient();
  client.setQueryData(preferenceKey('view:backlog'), { columns: ['a'] });
  mount(<SessionsPanel />, client);

  await click(ids.resetViews);

  await vi.waitFor(() =>
    expect(client.getQueryState(preferenceKey('view:backlog'))?.isInvalidated).toBe(true),
  );
});

test('Projects_WhenLeft_ShouldRefreshProjectsAndMe', async () => {
  const sent = stubFetch();
  const client = new QueryClient();
  client.setQueryData(meKey, { ...me, hasProjects: true });
  mount(<MyProjectsPanel />, client);
  await vi.waitFor(() => expect(client.getQueryData(projectsKey)).toBeDefined());

  await click(ids.myProjectLeave);
  await click(ids.myProjectLeaveConfirm);

  await vi.waitFor(() => expect(client.getQueryState(meKey)?.isInvalidated).toBe(true));
  expect(sent.filter((s) => s === 'GET /api/projects')).toHaveLength(2);
});
