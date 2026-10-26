import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeEach, expect, test } from 'vitest';
import { projectMembersKey } from '../queries/shared';
import { useRefreshMembersOnArrival } from './useRefreshMembersOnArrival';

const projectId = '0199a000-0000-7000-8000-000000000012';

function Probe({ userIds }: { userIds: readonly string[] }) {
  useRefreshMembersOnArrival(projectId, userIds);
  return null;
}

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
});

function setup() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  queryClient.setQueryData(projectMembersKey(projectId), []);
  const root = createRoot(document.createElement('div'));
  const show = (userIds: readonly string[]) =>
    act(() =>
      root.render(
        <QueryClientProvider client={queryClient}>
          <Probe userIds={userIds} />
        </QueryClientProvider>,
      ),
    );
  const stale = () => queryClient.getQueryState(projectMembersKey(projectId))?.isInvalidated;
  return { show, stale };
}

test('refreshMembers_WhenSomeoneArrives_ShouldInvalidateMembers', () => {
  const { show, stale } = setup();
  show(['user-1']);
  expect(stale()).toBe(false);

  show(['user-1', 'user-2']);

  expect(stale()).toBe(true);
});

test('refreshMembers_WhenSameIdsInOtherOrder_ShouldLeaveMembersAlone', () => {
  const { show, stale } = setup();
  show(['user-1', 'user-2']);

  show(['user-2', 'user-1']);

  expect(stale()).toBe(false);
});

test('refreshMembers_WhenSomeoneLeaves_ShouldLeaveMembersAlone', () => {
  const { show, stale } = setup();
  show(['user-1', 'user-2']);

  show(['user-1']);

  expect(stale()).toBe(false);
});
