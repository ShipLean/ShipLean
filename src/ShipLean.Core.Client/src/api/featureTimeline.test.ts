import { afterEach, describe, expect, it, vi } from 'vitest';
import { ok, sent, stubFetch } from '../test-support/fetchStub';
import { featureTimelineKeys, getFeatureTimeline } from './featureTimeline';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('featureTimeline', () => {
  it('getFeatureTimeline GETs the timeline route with both ids encoded', async () => {
    const stub = stubFetch(ok());
    await getFeatureTimeline('p/1', 'SHIP 1');
    expect(sent(stub)).toEqual({
      url: '/api/projects/p%2F1/work-items/by-key/SHIP%201/timeline',
      method: 'GET',
      body: undefined,
    });
  });

  it('featureTimelineKeys sit under the project', () => {
    expect(featureTimelineKeys.timeline('p', 'K')).toEqual([
      'projects',
      'p',
      'feature-timeline',
      'by-key',
      'K',
    ]);
  });
});
