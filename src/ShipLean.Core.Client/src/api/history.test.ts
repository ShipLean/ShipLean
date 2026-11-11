import { afterEach, describe, expect, it, vi } from 'vitest';
import { getHistory, historyKeys } from './history';
import { ok, sent, stubFetch } from '../test-support/fetchStub';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('history', () => {
  it('getHistory GETs the history route', async () => {
    const stub = stubFetch(ok({ revisions: [], asks: [] }));
    await getHistory('a/b');
    expect(sent(stub)).toEqual({
      url: '/api/work-items/a%2Fb/history',
      method: 'GET',
      body: undefined,
    });
    expect(historyKeys.item('x')).toEqual(['history', 'x']);
  });
});
