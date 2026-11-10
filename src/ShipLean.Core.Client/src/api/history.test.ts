import { afterEach, describe, expect, it, vi } from 'vitest';
import { getHistory, historyKeys } from './history';

function stubFetch(response: Response) {
  const fetchStub = vi.fn().mockResolvedValue(response);
  vi.stubGlobal('fetch', fetchStub);
  return fetchStub;
}

function sent(fetchStub: ReturnType<typeof stubFetch>) {
  const [url, init] = fetchStub.mock.calls[0] as [string, RequestInit];
  return {
    url,
    method: init.method,
    body: init.body === undefined ? undefined : (JSON.parse(String(init.body)) as unknown),
  };
}

const ok = (body: unknown = {}, status = 200) => new Response(JSON.stringify(body), { status });

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
