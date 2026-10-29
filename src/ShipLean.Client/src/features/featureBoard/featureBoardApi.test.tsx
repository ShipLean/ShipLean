import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { taskboardKey, useCreateOnTaskboard } from '../taskboard/taskboardApi';
import { useFeatureBoard } from './featureBoardApi';

const reads = vi.hoisted(() => ({ paths: [] as string[] }));
vi.mock('../../api/client', async (original) => ({
  ...(await original<typeof import('../../api/client')>()),
  api: {
    get: vi.fn(async (path: string) => {
      reads.paths.push(path);
      return { featureId: 'f1' };
    }),
    post: vi.fn(async () => ({})),
  },
}));

const projectId = '0199a000-0000-7000-8000-000000000012';
const featureScope = { kind: 'feature', featureId: 'f1' } as const;

type Create = ReturnType<typeof useCreateOnTaskboard>['mutate'];
let create: Create | undefined;

function Screen() {
  const { scope } = useFeatureBoard(projectId, 'SHIP-101');
  const { mutate } = useCreateOnTaskboard(projectId, scope ?? featureScope);
  useEffect(() => {
    create = mutate;
  }, [mutate]);
  return null;
}

let root: Root;
let queryClient: QueryClient;
beforeEach(async () => {
  vi.useFakeTimers();
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  reads.paths = [];
  queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  root = createRoot(document.createElement('div'));
  act(() => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <Screen />
      </QueryClientProvider>,
    );
  });
  await act(() => vi.advanceTimersByTimeAsync(0));
});
afterEach(() => {
  act(() => root.unmount());
  vi.useRealTimers();
});

test('useFeatureBoard_WhenTheFirstReadAnswers_ShouldSeedTheScopeKeyWithoutASecondGet', () => {
  expect(reads.paths).toEqual([
    `/api/projects/${projectId}/work-items/by-key/SHIP-101/feature-board`,
  ]);
  expect(queryClient.getQueryData(taskboardKey(projectId, featureScope))).toEqual({
    featureId: 'f1',
  });
});

test('useCreateOnTaskboard_WhenOnAFeatureBoard_ShouldInvalidateTheFeatureScopeKey', async () => {
  act(() => create?.({ type: 'story', title: 's', parentId: null }));
  await act(() => vi.advanceTimersByTimeAsync(0));

  // The write refetches the board under the scope key: a second GET of the same by-key path.
  expect(reads.paths).toHaveLength(2);
  expect(queryClient.getQueryState(taskboardKey(projectId, featureScope))?.isInvalidated).toBe(
    false,
  );
});
