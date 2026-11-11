import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import runJson from '../../../../../../ShipLean.TestSupport/PinnedShapes/backup-run.json';
import { backupsKey, useRunBackupNow } from './backupsApi';

// No testing library here: a probe component hands the mutation out, and fetch answers like the server.
let client: QueryClient;
let fetchCalls: { url: string; method: string }[];

const answer = (status: number, body: unknown) =>
  vi.fn((url: string, init: { method: string }) => {
    fetchCalls.push({ url, method: init.method });
    return Promise.resolve(
      new Response(JSON.stringify(body), {
        status,
        headers: { 'content-type': 'application/json' },
      }),
    );
  });

function startRun() {
  let mutation: ReturnType<typeof useRunBackupNow> | undefined;
  function Probe() {
    mutation = useRunBackupNow();
    return null;
  }
  const host = document.createElement('div');
  act(() => {
    createRoot(host).render(
      <QueryClientProvider client={client}>
        <Probe />
      </QueryClientProvider>,
    );
  });
  return () => mutation!.mutateAsync();
}

beforeEach(() => {
  (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  fetchCalls = [];
});

afterEach(() => vi.unstubAllGlobals());

test('useRunBackupNow_WhenTheRunStarts_ShouldPostAndReloadTheSettings', async () => {
  vi.stubGlobal('fetch', answer(202, runJson.running));
  const invalidate = vi.spyOn(client, 'invalidateQueries');
  const run = startRun();
  await act(async () => {
    await run();
  });
  expect(fetchCalls).toEqual([{ url: '/api/instance/backups/run', method: 'POST' }]);
  expect(invalidate).toHaveBeenCalledWith({ queryKey: backupsKey, exact: true });
});

test('useRunBackupNow_WhenAnotherRunIsGoing_ShouldNotReloadTheSettings', async () => {
  vi.stubGlobal('fetch', answer(409, { error: 'A backup is already running.', code: 'conflict' }));
  const invalidate = vi.spyOn(client, 'invalidateQueries');
  const run = startRun();
  await act(async () => {
    await expect(run()).rejects.toThrow('A backup is already running.');
  });
  expect(invalidate).not.toHaveBeenCalled();
});
