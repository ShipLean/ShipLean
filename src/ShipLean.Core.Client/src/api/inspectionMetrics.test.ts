import { afterEach, describe, expect, it, vi } from 'vitest';
import { ok, sent, stubFetch } from '../test-support/fetchStub';
import { getInspectionMetrics, inspectionMetricsKeys } from './inspectionMetrics';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('inspectionMetrics', () => {
  it('getInspectionMetrics GETs the route with no query when none is given', async () => {
    const stub = stubFetch(ok());
    await getInspectionMetrics('p/1');
    expect(sent(stub)).toEqual({
      url: '/api/projects/p%2F1/inspection-metrics',
      method: 'GET',
      body: undefined,
    });
  });

  it('getInspectionMetrics sends every query value it is given, encoded', async () => {
    const stub = stubFetch(ok());
    await getInspectionMetrics('p', {
      range: 'lastQuarter',
      cut: 'feature',
      period: 's 1',
      select: 'SHIP-1',
    });
    expect(sent(stub).url).toBe(
      '/api/projects/p/inspection-metrics?range=lastQuarter&cut=feature&period=s+1&select=SHIP-1',
    );
  });

  it('inspectionMetricsKeys sit under the project with the query', () => {
    expect(inspectionMetricsKeys.view('p', { range: 'all' })).toEqual([
      'projects',
      'p',
      'inspection-metrics',
      { range: 'all' },
    ]);
  });
});
