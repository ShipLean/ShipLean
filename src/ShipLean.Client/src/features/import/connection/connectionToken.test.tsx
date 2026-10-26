import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { expect, test, vi } from 'vitest';
import { importTestIds as ids } from '../testIds';
import { ConnectionForm } from './ConnectionForm';

const SECRET = 'SECRET-TOKEN';

const cachedVariables = (client: QueryClient) =>
  JSON.stringify(
    client
      .getMutationCache()
      .getAll()
      .map((m) => m.state.variables ?? null),
  );

function type(testId: string, value: string) {
  const input = document.querySelector<HTMLInputElement>(`[data-testid="${testId}"]`)!;
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value')!.set!;
  act(() => {
    setter.call(input, value);
    input.dispatchEvent(new Event('input', { bubbles: true }));
  });
}

test('connectionForm_WhenSavedAndUnmounted_ShouldKeepNoTokenInTheMutationCache', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  vi.stubGlobal('fetch', () =>
    Promise.resolve(
      new Response(JSON.stringify({ id: 'c1' }), {
        status: 200,
        headers: { 'content-type': 'application/json' },
      }),
    ),
  );
  const client = new QueryClient();
  const container = document.body.appendChild(document.createElement('div'));
  const root = createRoot(container);
  act(() =>
    root.render(
      <QueryClientProvider client={client}>
        <MemoryRouter>
          <ConnectionForm projectId="p1" saved={null} />
        </MemoryRouter>
      </QueryClientProvider>,
    ),
  );
  type(ids.baseUrl, 'https://a.atlassian.net');
  type(ids.email, 'a@b.io');
  type(ids.token, SECRET);
  expect(cachedVariables(client)).not.toContain(SECRET);
  await act(async () => {
    document.querySelector<HTMLButtonElement>(`[data-testid="${ids.save}"]`)!.click();
  });
  await vi.waitFor(() => expect(client.isMutating()).toBe(0));
  // Still mounted: the form has dropped the token and the mutation's variables.
  expect(cachedVariables(client)).not.toContain(SECRET);
  act(() => root.unmount());
  expect(cachedVariables(client)).not.toContain(SECRET);
  expect(client.getMutationCache().getAll()).toHaveLength(0);
});
