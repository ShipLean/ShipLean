import { act, useEffect } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { api } from '../../../api/client';
import { BULK_MAX_ROWS, type BulkWriteRequest } from '../../../api/backlogWrites';
import { backlogView, itemRow } from '../ct/fixtures';
import { backlogKey, backlogRootKey } from '../../../queries/backlogKeys';
import { moveToEdgePlan } from './movePlan';
import { useBulkRun, type BulkRunner } from './useBulkRun';

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
vi.mock('../../../api/client', () => ({ api: { post: vi.fn() } }));

const post = vi.mocked(api.post);
const key = backlogKey('p1', 'stories', 'list', '', false);
let queryClient: QueryClient;
let runner: BulkRunner;
let root: Root;

function Probe() {
  const current = useBulkRun('p1', key);
  useEffect(() => {
    runner = current;
  });
  return null;
}

beforeEach(() => {
  post.mockReset();
  queryClient = new QueryClient();
  root = createRoot(document.createElement('div'));
  act(() =>
    root.render(
      <QueryClientProvider client={queryClient}>
        <Probe />
      </QueryClientProvider>,
    ),
  );
});
afterEach(() => act(() => root.unmount()));

const rows = [itemRow({ key: 'SHIP-1', revision: 1 }), itemRow({ key: 'SHIP-2', revision: 1 })];
const written = (id: string) => ({ id, key: id, revision: 2, changes: [], notices: [] });
const sentBody = () => post.mock.calls[0][1] as BulkWriteRequest;

test('posts_every_row_once', async () => {
  queryClient.setQueryData(key, backlogView(rows));
  post.mockResolvedValue({ written: rows.map((r) => written(r.id)), stale: [] });

  await act(() => runner.run(moveToEdgePlan(rows, 'last')));

  expect(post).toHaveBeenCalledTimes(1);
  expect(post.mock.calls[0][0]).toBe('/api/projects/p1/backlog/bulk');
  expect(sentBody().items.map((i) => i.id)).toEqual(rows.map((r) => r.id));
});

test('sends_the_revision_in_the_cache_at_send_time', async () => {
  const plan = moveToEdgePlan(rows, 'first');
  queryClient.setQueryData(key, backlogView(rows.map((r) => ({ ...r, revision: 7 }))));
  post.mockResolvedValue({ written: [], stale: [] });

  await act(() => runner.run(plan));

  expect(sentBody().items.map((i) => i.expectedRevision)).toEqual([7, 7]);
});

test('reports_written_and_stale', async () => {
  const view = backlogView(rows);
  queryClient.setQueryData(key, view);
  const stale = { error: 'conflict', code: 'conflict' as const, current: {}, theirChanges: [] };
  post.mockResolvedValue({ written: [written(rows[0].id)], stale: [stale] });

  const outcome = await act(() => runner.run(moveToEdgePlan(rows, 'first')));

  expect(outcome.written.map((w) => w.id)).toEqual([rows[0].id]);
  expect(outcome.stale).toHaveLength(1);
  expect(outcome.viewBefore).toBe(view);
  expect(runner.outcome).toBe(outcome);
  act(() => runner.dismiss());
  expect(runner.outcome).toBeNull();
});

test('invalidates_the_backlog_after_a_partial_answer', async () => {
  queryClient.setQueryData(key, backlogView(rows));
  const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
  const stale = { error: 'conflict', code: 'conflict' as const, current: {}, theirChanges: [] };
  post.mockResolvedValue({ written: [], stale: [stale, stale] });

  await act(() => runner.run(moveToEdgePlan(rows, 'first')));

  expect(invalidate).toHaveBeenCalledWith({ queryKey: backlogRootKey('p1') });
});

test('refuses_over_100_without_a_request', async () => {
  const many = Array.from({ length: BULK_MAX_ROWS + 1 }, (_, i) => itemRow({ key: `SHIP-${i}` }));

  const outcome = await act(() => runner.run(moveToEdgePlan(many, 'first')));

  expect(post).not.toHaveBeenCalled();
  expect(outcome.written).toEqual([]);
});
