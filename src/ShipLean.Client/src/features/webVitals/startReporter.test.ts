import { expect, test, vi } from 'vitest';
import { startReporter } from './startReporter';

const mocks = vi.hoisted(() => ({
  onLCP: vi.fn(),
  onINP: vi.fn(),
  onCLS: vi.fn(),
  onFCP: vi.fn(),
  onTTFB: vi.fn(),
  post: vi.fn(),
}));

vi.mock('web-vitals', () => ({
  onLCP: mocks.onLCP,
  onINP: mocks.onINP,
  onCLS: mocks.onCLS,
  onFCP: mocks.onFCP,
  onTTFB: mocks.onTTFB,
}));

vi.mock('./webVitalsApi', () => ({ postWebVitals: mocks.post }));

function hide() {
  Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true });
  document.dispatchEvent(new Event('visibilitychange'));
}

test('startReporter_WhenCalledTwice_ShouldMeasureAndSendOnce', () => {
  history.replaceState(null, '', '/p/SHIP/backlogs/stories');
  startReporter();
  startReporter();

  for (const on of [mocks.onLCP, mocks.onINP, mocks.onCLS]) {
    expect(on).toHaveBeenCalledTimes(1);
    expect(on.mock.calls[0]?.[1]).toEqual({ reportSoftNavs: true });
  }
  for (const on of [mocks.onFCP, mocks.onTTFB]) {
    expect(on).toHaveBeenCalledTimes(1);
    expect(on.mock.calls[0]?.[1]).toBeUndefined();
  }

  const report = mocks.onLCP.mock.calls[0]?.[0] as (metric: object) => void;
  report({ name: 'LCP', value: 812.4, navigationType: 'navigate', navigationId: 1 });
  hide();

  expect(mocks.post).toHaveBeenCalledTimes(1);
  expect(mocks.post.mock.calls[0]?.[0]).toMatchObject([
    { screen: 'storiesBacklog', navigation: 'hard', lcp: 812.4 },
  ]);
});
