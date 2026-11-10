import { afterEach, describe, expect, it, vi } from 'vitest';
import { retroKeys, supportPoint, unsupportPoint } from './retrosApi';

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

const noContent = () => new Response(null, { status: 204 });

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('retrosApi', () => {
  it('supportPoint POSTs and unsupportPoint DELETEs the same route with no body', async () => {
    const post = stubFetch(noContent());
    await expect(supportPoint('p/1')).resolves.toBeUndefined();
    const del = stubFetch(noContent());
    await expect(unsupportPoint('p/1')).resolves.toBeUndefined();
    expect(sent(post)).toEqual({
      url: '/api/retros/points/p%2F1/support',
      method: 'POST',
      body: undefined,
    });
    expect(sent(del)).toEqual({
      url: '/api/retros/points/p%2F1/support',
      method: 'DELETE',
      body: undefined,
    });
  });

  it('retroKeys differ by id, range and query', () => {
    expect(retroKeys.sprint('s')).toEqual(['retro', 'sprint', 's']);
    expect(retroKeys.insights('p', 'all')).not.toEqual(retroKeys.insights('p', '6'));
    expect(retroKeys.features('p')).toEqual(['retro', 'p', 'features']);
    expect(retroKeys.actionDialog('p', { itemId: 'a' })).not.toEqual(
      retroKeys.actionDialog('p', { itemId: 'b' }),
    );
  });
});
