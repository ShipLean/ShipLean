import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import type { ProjectEvent } from '../../../api/events';
import { importJob } from '../ct/fixtures';
import { importJobKey } from './runApi';

type Handler = (event: ProjectEvent) => void;
const handlers: Handler[] = [];
vi.mock('../../../realtime', () => ({
  registerInvalidation: (type: string, handler: Handler) => {
    if (type === 'importProgress') handlers.push(handler);
    return () => {};
  },
}));

const { useImportJob } = await import('./runApi');

function Probe({ jobId }: { jobId: string }) {
  useImportJob('p-ship', jobId);
  return null;
}

let reads: string[] = [];
let answer = importJob();

function mountProbe() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const root = createRoot(document.createElement('div'));
  act(() =>
    root.render(
      <QueryClientProvider client={client}>
        <Probe jobId="job1" />
      </QueryClientProvider>,
    ),
  );
  return client;
}

const settle = () => act(() => vi.advanceTimersByTimeAsync(0));
const progress = (jobId: string) =>
  ({ type: 'importProgress', projectId: 'p-ship', clientId: null, jobId }) as ProjectEvent;

beforeEach(() => {
  handlers.length = 0;
  reads = [];
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  vi.useFakeTimers();
  vi.stubGlobal('fetch', async (url: string) => {
    reads.push(url);
    return new Response(JSON.stringify(answer), {
      status: 200,
      headers: { 'content-type': 'application/json' },
    });
  });
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

test('useImportJob_WhileTheJobHasNoProject_ShouldPoll', async () => {
  answer = importJob({ projectId: null });
  mountProbe();
  await settle();
  expect(reads).toHaveLength(1);
  await act(() => vi.advanceTimersByTimeAsync(6000));
  expect(reads.length).toBeGreaterThanOrEqual(3);
});

test('useImportJob_OnceTheJobHasAProject_ShouldStopPollingAndWaitForEvents', async () => {
  answer = importJob({ projectId: 'p-ship' });
  const client = mountProbe();
  await settle();
  await act(() => vi.advanceTimersByTimeAsync(20000));
  expect(reads).toHaveLength(1);

  const spy = vi.spyOn(client, 'invalidateQueries');
  act(() => handlers.forEach((h) => h(progress('job1'))));
  expect(spy.mock.calls.map(([f]) => f?.queryKey)).toContainEqual(importJobKey('p-ship', 'job1'));
  await settle();
  expect(reads).toHaveLength(2);
});

test('useImportJob_WhenAnotherJobReportsProgress_ShouldNotRefetch', async () => {
  answer = importJob({ projectId: 'p-ship' });
  mountProbe();
  await settle();
  act(() => handlers.forEach((h) => h(progress('other'))));
  await settle();
  expect(reads).toHaveLength(1);
});

test('useImportJob_WhenTheJobFinishedWithoutAProject_ShouldNotPoll', async () => {
  answer = importJob({ projectId: null, status: 'failed' });
  mountProbe();
  await settle();
  await act(() => vi.advanceTimersByTimeAsync(20000));
  expect(reads).toHaveLength(1);
});
