import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeEach, expect, test, vi } from 'vitest';
import { useWebVitals } from './useWebVitals';
import { webVitalsConfigKey } from './webVitalsApi';

const mocks = vi.hoisted(() => ({
  getConfig: vi.fn(),
  start: vi.fn(),
  chunkFails: false,
}));

vi.mock('./webVitalsApi', () => ({
  webVitalsConfigKey: ['webVitalsConfig'],
  getWebVitalsConfig: mocks.getConfig,
}));

// A chunk an ad blocker refused fails the dynamic import; reading the export here fails it the same way.
vi.mock('./startReporter', () => ({
  get startReporter() {
    if (mocks.chunkFails) throw new Error('Failed to fetch dynamically imported module');
    return mocks.start;
  },
}));

beforeEach(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  mocks.getConfig.mockReset();
  mocks.start.mockReset();
  mocks.chunkFails = false;
});

function Probe() {
  useWebVitals();
  return null;
}

async function mount() {
  const client = new QueryClient();
  await act(async () => {
    createRoot(document.createElement('div')).render(
      <QueryClientProvider client={client}>
        <Probe />
      </QueryClientProvider>,
    );
  });
  return client;
}

// For the cases where nothing is expected: the config query has answered and every dynamic import has settled.
async function settle(client: QueryClient) {
  await vi.waitFor(() =>
    expect(client.getQueryState(webVitalsConfigKey)?.status).not.toBe('pending'),
  );
  await vi.dynamicImportSettled();
}

test('useWebVitals_WhenEnabled_ShouldLoadTheReporter', async () => {
  mocks.getConfig.mockResolvedValue({ enabled: true });
  await mount();
  await vi.waitFor(() => expect(mocks.start).toHaveBeenCalledTimes(1));
  expect(mocks.getConfig).toHaveBeenCalledTimes(1);
});

test('useWebVitals_WhenNotEnabled_ShouldNotLoadTheReporter', async () => {
  mocks.getConfig.mockResolvedValue({ enabled: false });
  await settle(await mount());
  expect(mocks.getConfig).toHaveBeenCalledTimes(1);
  expect(mocks.start).not.toHaveBeenCalled();
});

test('useWebVitals_WhenTheChunkFailsToLoad_ShouldStaySilent', async () => {
  mocks.getConfig.mockResolvedValue({ enabled: true });
  mocks.chunkFails = true;
  const unhandled = vi.fn();
  process.on('unhandledRejection', unhandled);
  await settle(await mount());
  process.off('unhandledRejection', unhandled);
  expect(mocks.start).not.toHaveBeenCalled();
  expect(unhandled).not.toHaveBeenCalled();
});

test('useWebVitals_WhenConfigFails_ShouldStaySilent', async () => {
  mocks.getConfig.mockRejectedValue(new Error('500'));
  await settle(await mount());
  expect(mocks.getConfig).toHaveBeenCalledTimes(1);
  expect(mocks.start).not.toHaveBeenCalled();
});
