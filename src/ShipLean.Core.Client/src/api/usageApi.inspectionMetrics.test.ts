import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  postInspectionMetricsPeriodOpened,
  postInspectionMetricsTaskOpened,
  postInspectionMetricsViewed,
} from './usageApi';
import { noContent, sent, stubFetch } from '../test-support/fetchStub';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('inspection metrics usage', () => {
  it('postInspectionMetricsViewed POSTs the view', async () => {
    const fetchStub = stubFetch(noContent());
    await postInspectionMetricsViewed('p/1', 'leadtime');
    expect(sent(fetchStub)).toEqual({
      url: '/api/projects/p%2F1/usage/inspection-metrics-viewed',
      method: 'POST',
      body: { view: 'leadtime' },
    });
  });

  it('postInspectionMetricsPeriodOpened POSTs the kind of sprint', async () => {
    const fetchStub = stubFetch(noContent());
    await postInspectionMetricsPeriodOpened('p/1', 'running');
    expect(sent(fetchStub)).toEqual({
      url: '/api/projects/p%2F1/usage/inspection-metrics-period-opened',
      method: 'POST',
      body: { period: 'running' },
    });
  });

  it('postInspectionMetricsTaskOpened POSTs where the task was opened from', async () => {
    const fetchStub = stubFetch(noContent());
    await postInspectionMetricsTaskOpened('p/1', 'rail');
    expect(sent(fetchStub)).toEqual({
      url: '/api/projects/p%2F1/usage/inspection-metrics-task-opened',
      method: 'POST',
      body: { from: 'rail' },
    });
  });
});
