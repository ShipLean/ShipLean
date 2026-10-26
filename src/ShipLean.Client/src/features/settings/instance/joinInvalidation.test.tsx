import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { expect, test, vi } from 'vitest';

vi.mock('../../../api/client', async (original) => ({
  ...(await original<typeof import('../../../api/client')>()),
  api: { post: vi.fn().mockResolvedValue(undefined) },
}));

const { useJoinAsAdmin, instanceProjectsKey } = await import('./instanceApi');
const { instanceUsersKey } = await import('../instanceUsers/usersAdminApi');

test('useJoinAsAdmin_WhenJoined_ShouldReadEveryListThatShowsMembershipsAgain', async () => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const client = new QueryClient();
  const spy = vi.spyOn(client, 'invalidateQueries');
  let join: ReturnType<typeof useJoinAsAdmin> | undefined;
  function Probe() {
    join = useJoinAsAdmin();
    return null;
  }
  const root = createRoot(document.createElement('div'));
  act(() =>
    root.render(
      <QueryClientProvider client={client}>
        <Probe />
      </QueryClientProvider>,
    ),
  );

  await act(async () => {
    await join?.mutateAsync('p-1');
  });

  const keys = spy.mock.calls.map(([filters]) => filters?.queryKey);
  expect(keys).toEqual(
    expect.arrayContaining([instanceProjectsKey, instanceUsersKey, ['projects'], ['me']]),
  );
});
