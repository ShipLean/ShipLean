import { afterEach, describe, expect, it, vi } from 'vitest';
import { featureBoardKeys, getFeatureBoard } from './featureBoard';

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

describe('featureBoard', () => {
  it('getFeatureBoard GETs the feature-board route with both ids encoded', async () => {
    const stub = stubFetch(ok());
    await getFeatureBoard('p/1', 'SHIP 1');
    expect(sent(stub)).toEqual({
      url: '/api/projects/p%2F1/work-items/by-key/SHIP%201/feature-board',
      method: 'GET',
      body: undefined,
    });
    expect(featureBoardKeys.board('p', 'K')).toEqual(['featureBoard', 'p', 'K']);
  });
});
