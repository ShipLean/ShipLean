import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

const puts: unknown[] = [];
vi.mock('../../api/client', async (original) => ({
  ...(await original<typeof import('../../api/client')>()),
  api: {
    get: vi.fn(() => Promise.resolve(undefined)),
    put: vi.fn((_url: string, body: unknown) => {
      puts.push(body);
      return Promise.resolve();
    }),
  },
}));

const { BACKLOG_COLUMNS, backlogViewKey, hiddenColumnsOf, useBacklogView } =
  await import('./backlogApi');

const FEATURE_COLUMNS = [
  { id: 'key', label: 'Key', required: true },
  { id: 'progress', label: 'Progress', required: false },
  { id: 'state', label: 'State', required: false },
] as const;

beforeEach(() => {
  puts.length = 0;
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
});
afterEach(() => {
  vi.useRealTimers();
});

// React Query tells a hook its cached value changed on a setTimeout(0) (its notifyManager), which a plain `await act`
// can finish before. Running that timer inside act re-renders the hooks before anything is asserted.
async function write(change: () => void) {
  await act(async () => {
    change();
    await vi.advanceTimersByTimeAsync(0);
  });
}

test('hiddenColumnsOf_WhenLevelHasItsOwnColumns_ShouldKeepThem', () => {
  const pref = { hiddenColumns: { features: ['progress', 'state', 'key', 'sprint'] } };
  expect(hiddenColumnsOf(pref, 'features', FEATURE_COLUMNS)).toEqual(['progress', 'state']);
  expect(hiddenColumnsOf(pref, 'stories', BACKLOG_COLUMNS)).toEqual([]);
});

test('hiddenColumnsOf_WhenRankOrKeyOrTitleSaved_ShouldNeverHideThem', () => {
  const pref = { hiddenColumns: { stories: ['rank', 'key', 'title', 'tags'] } };
  expect(hiddenColumnsOf(pref, 'stories', BACKLOG_COLUMNS)).toEqual(['tags']);
});

test('hiddenColumnsOf_WhenNothingSaved_ShouldHideNothing', () => {
  expect(hiddenColumnsOf(null, 'epics', FEATURE_COLUMNS)).toEqual([]);
  expect(hiddenColumnsOf({}, 'epics', FEATURE_COLUMNS)).toEqual([]);
});

test('useBacklogView_WhenTwoWritersSave_ShouldKeepBothChanges', async () => {
  vi.useFakeTimers();
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  queryClient.setQueryData(backlogViewKey, {});
  let stories!: ReturnType<typeof useBacklogView>;
  let features!: ReturnType<typeof useBacklogView>;
  function Writers() {
    stories = useBacklogView('stories', BACKLOG_COLUMNS);
    features = useBacklogView('features', FEATURE_COLUMNS);
    return null;
  }
  const root = createRoot(document.createElement('div'));
  await act(async () => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <Writers />
      </QueryClientProvider>,
    );
  });

  await write(() => stories.setHidden(['tags']));
  await write(() => features.setHidden(['progress']));
  await write(() =>
    stories.update({ hiddenColumns: { stories: ['tags'], features: ['progress'] } }),
  );

  expect(puts[0]).toEqual({ hiddenColumns: { stories: ['tags'] } });
  expect(puts[1]).toEqual({ hiddenColumns: { stories: ['tags'], features: ['progress'] } });
  expect(queryClient.getQueryData(backlogViewKey)).toEqual(puts[2]);
  // Query observers re-render on notifyManager's setTimeout tick, which act() doesn't flush.
  await vi.waitFor(() => {
    expect(features.hidden).toEqual(['progress']);
    expect(stories.hidden).toEqual(['tags']);
  });
  act(() => root.unmount());
});

test('useBacklogView_WhenUpdatePatchesAnotherField_ShouldKeepHiddenColumns', async () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: Infinity } },
  });
  queryClient.setQueryData(backlogViewKey, { hiddenColumns: { stories: ['tags'] } });
  let view!: ReturnType<typeof useBacklogView>;
  function Writer() {
    view = useBacklogView('stories', BACKLOG_COLUMNS);
    return null;
  }
  const root = createRoot(document.createElement('div'));
  await act(async () => {
    root.render(
      <QueryClientProvider client={queryClient}>
        <Writer />
      </QueryClientProvider>,
    );
  });
  await act(async () => view.update({ forecast: false }));
  expect(puts[0]).toEqual({ hiddenColumns: { stories: ['tags'] }, forecast: false });
  act(() => root.unmount());
});
