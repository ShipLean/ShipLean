import { expect, test, vi } from 'vitest';
import { createReportBuffer } from './reportBuffer';
import type { VitalsMetric } from './reportBuffer';
import type { WebVitalsReport } from './webVitalsApi';

const backlog = '/p/SHIP/backlogs/stories';
const workItem = 'https://shiplean.example/p/SHIP/items/SHIP-12?item=SHIP-13';

function setUp() {
  const send = vi.fn<(reports: WebVitalsReport[]) => void>();
  const buffer = createReportBuffer({ startPathname: backlog, screenSize: '1920x1080', send });
  const sent = () => send.mock.calls.flatMap(([reports]) => reports);
  return { buffer, send, sent };
}

const hard = (name: VitalsMetric['name'], value: number): VitalsMetric => ({
  name,
  value,
  navigationType: 'navigate',
  navigationId: 1,
});

test('reportBuffer_WhenSameMetricTwice_ShouldKeepTheFirst', () => {
  const { buffer, sent } = setUp();
  buffer.add(hard('LCP', 812.4));
  buffer.add(hard('CLS', 0.02));
  buffer.add(hard('CLS', 0.5));
  buffer.add(hard('LCP', 2000));
  buffer.flush();
  expect(sent()).toEqual([
    {
      screen: 'storiesBacklog',
      navigation: 'hard',
      lcp: 812.4,
      inp: null,
      cls: 0.02,
      fcp: null,
      ttfb: null,
      screenSize: '1920x1080',
    },
  ]);
});

test('reportBuffer_WhenSoftNavigation_ShouldLabelSoftAndOmitTtfb', () => {
  const { buffer, sent } = setUp();
  const soft = { navigationType: 'soft-navigation', navigationId: 2, navigationURL: workItem };
  buffer.add({ name: 'TTFB', value: 0, ...soft });
  buffer.add({ name: 'LCP', value: 300, ...soft });
  buffer.flush();
  // The real path holds a work item key and a query: only the route id may leave (D6).
  expect(sent()).toEqual([
    {
      screen: 'workItem',
      navigation: 'soft',
      lcp: 300,
      inp: null,
      cls: null,
      fcp: null,
      ttfb: null,
      screenSize: '1920x1080',
    },
  ]);
  expect(JSON.stringify(sent())).not.toContain('SHIP-12');
});

test('reportBuffer_WhenRouteUnknown_ShouldDropTheMetric', () => {
  const { buffer, send, sent } = setUp();
  buffer.add({
    name: 'LCP',
    value: 300,
    navigationType: 'soft-navigation',
    navigationId: 2,
    navigationURL: '/nowhere',
  });
  buffer.flush();
  expect(send).not.toHaveBeenCalled();
  // Dropped, not taken: a later metric for a known screen is still reported.
  buffer.add(hard('LCP', 400));
  buffer.flush();
  expect(sent()).toMatchObject([{ screen: 'storiesBacklog', lcp: 400 }]);
});

test('reportBuffer_WhenOver20Reports_ShouldFlush', () => {
  const { buffer, send } = setUp();
  for (let id = 1; id <= 19; id++) buffer.add({ ...hard('LCP', id), navigationId: id });
  expect(send).not.toHaveBeenCalled();
  buffer.add({ ...hard('LCP', 20), navigationId: 20 });
  expect(send).toHaveBeenCalledTimes(1);
  const [first] = send.mock.calls[0] ?? [];
  expect(first).toHaveLength(20);
  expect(first?.map((r) => r.lcp)).toEqual(Array.from({ length: 20 }, (_, i) => i + 1));
  buffer.add({ ...hard('LCP', 21), navigationId: 21 });
  buffer.flush();
  expect(send).toHaveBeenCalledTimes(2);
  expect(send.mock.calls[1]?.[0]).toHaveLength(1);
});

test('reportBuffer_WhenFlushed_ShouldNotResendAMetric', () => {
  const { buffer, send, sent } = setUp();
  buffer.add(hard('LCP', 812.4));
  buffer.add(hard('INP', 96));
  buffer.flush();
  buffer.add(hard('INP', 400));
  buffer.add(hard('CLS', 0.1));
  buffer.flush();
  buffer.flush();
  expect(send).toHaveBeenCalledTimes(2);
  expect(sent()).toMatchObject([
    { navigation: 'hard', lcp: 812.4, inp: 96, cls: null },
    { navigation: 'hard', lcp: null, inp: null, cls: 0.1 },
  ]);
});
