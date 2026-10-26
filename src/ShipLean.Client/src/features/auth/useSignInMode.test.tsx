import { QueryClient, QueryClientProvider, notifyManager } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, test, vi } from 'vitest';
import { instanceKey, type InstanceDto } from './authApi';
import type { SignInMode } from './useSignInMode';

const fetchInstance = vi.fn<() => Promise<InstanceDto>>();
vi.mock('./authApi', async (original) => ({
  ...(await original<typeof import('./authApi')>()),
  fetchInstance: () => fetchInstance(),
}));

const { useSignInMode } = await import('./useSignInMode');

// Query updates reach the component at once instead of on the next tick, so an idle query means React has rendered it.
notifyManager.setScheduler((callback) => callback());

async function readMode(answer: Promise<InstanceDto>) {
  fetchInstance.mockReturnValueOnce(answer);
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const seen: SignInMode[] = [];
  const Probe = () => {
    seen.push(useSignInMode());
    return null;
  };
  const root = createRoot(document.createElement('div'));
  await act(async () => {
    root.render(
      <QueryClientProvider client={client}>
        <Probe />
      </QueryClientProvider>,
    );
    await answer.catch(() => undefined);
  });
  await settle(client);
  return { seen, client };
}

async function settle(client: QueryClient) {
  await act(() => vi.waitFor(() => expect(client.isFetching()).toBe(0)));
}

test('useSignInMode_WhenInstanceIsProxy_ShouldReportProxyAndSignOutUrl', async () => {
  const { seen } = await readMode(
    Promise.resolve({ name: 'Larkspur', signIn: 'proxy', signOutUrl: '/oauth2/sign_out' }),
  );

  expect(seen[0]).toEqual({ proxy: false, signOutUrl: null });
  expect(seen.at(-1)).toEqual({ proxy: true, signOutUrl: '/oauth2/sign_out' });
});

test('useSignInMode_WhenInstanceIsPassword_ShouldReportPassword', async () => {
  const { seen } = await readMode(
    Promise.resolve({ name: 'Larkspur', signIn: 'password', signOutUrl: null }),
  );

  expect(seen.at(-1)).toEqual({ proxy: false, signOutUrl: null });
});

test('useSignInMode_WhenInstanceFails_ShouldReportPassword', async () => {
  const { seen } = await readMode(Promise.reject(new Error('offline')));

  expect(seen.at(-1)).toEqual({ proxy: false, signOutUrl: null });
});

test('useSignInMode_WhenRefetchFailsAfterProxy_ShouldReportPassword', async () => {
  const { seen, client } = await readMode(
    Promise.resolve({ name: 'Larkspur', signIn: 'proxy', signOutUrl: '/oauth2/sign_out' }),
  );
  expect(seen.at(-1)).toEqual({ proxy: true, signOutUrl: '/oauth2/sign_out' });
  fetchInstance.mockReturnValueOnce(Promise.reject(new Error('offline')));

  await act(() => client.refetchQueries({ queryKey: instanceKey }));
  await settle(client);

  expect(client.getQueryData(instanceKey)).toBeDefined();
  expect(seen.at(-1)).toEqual({ proxy: false, signOutUrl: null });
});
