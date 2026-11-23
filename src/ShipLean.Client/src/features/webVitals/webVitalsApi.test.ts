import { afterEach, expect, test, vi } from 'vitest';
import { platform } from '@shiplean/core/platform';
import batchJson from '../../../../ShipLean.TestSupport/PinnedShapes/web-vitals-batch.json';
import configJson from '../../../../ShipLean.TestSupport/PinnedShapes/web-vitals-config.json';
import { postWebVitals } from './webVitalsApi';
import type { WebVitalsConfig, WebVitalsReport } from './webVitalsApi';

const keysOf = (value: object) => Object.keys(value).sort();
const roundTrip = (value: unknown) => JSON.parse(JSON.stringify(value)) as unknown;

const reportKeys = ['cls', 'fcp', 'inp', 'lcp', 'navigation', 'screen', 'screenSize', 'ttfb'];

// The JSON import types navigation as string; every other field is checked against the type by the assignment.
function pinnedReports(): WebVitalsReport[] {
  return batchJson.reports.map((report) => {
    const { navigation } = report;
    if (navigation !== 'hard' && navigation !== 'soft')
      throw new Error(`Unknown navigation ${navigation}`);
    return { ...report, navigation };
  });
}

afterEach(() => vi.unstubAllGlobals());

test('WebVitalsApi_WhenPinnedBatchRead_ShouldRoundTripThroughTheType', () => {
  const reports = pinnedReports();
  expect(reports.length).toBeGreaterThan(0);
  for (const report of reports) expect(keysOf(report)).toEqual(reportKeys);
  expect(roundTrip({ reports })).toEqual(batchJson);
});

test('WebVitalsApi_WhenPinnedConfigRead_ShouldRoundTripThroughTheType', () => {
  const config: WebVitalsConfig = configJson;
  expect(keysOf(config)).toEqual(['enabled']);
  expect(roundTrip(config)).toEqual(configJson);
});

test('postWebVitals_WhenCalled_ShouldSendKeepaliveJson', async () => {
  const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 204 }));
  vi.stubGlobal('fetch', fetchMock);
  const reports = pinnedReports();
  await postWebVitals(reports);
  expect(fetchMock).toHaveBeenCalledTimes(1);
  const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
  expect(url).toBe(platform().apiOrigin() + '/api/usage/web-vitals');
  expect(init.method).toBe('POST');
  expect(init.keepalive).toBe(true);
  expect(init.credentials).toBe('same-origin');
  expect(init.headers).toEqual({ 'Content-Type': 'application/json' });
  expect(JSON.parse(init.body as string)).toEqual(batchJson);
});

test('postWebVitals_WhenTheNetworkFails_ShouldNotThrow', async () => {
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));
  await expect(postWebVitals([])).resolves.toBeUndefined();
});
