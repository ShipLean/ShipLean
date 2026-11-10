import { afterEach, describe, expect, it, vi } from 'vitest';
import { addComment, archiveComment, commentKeys, listComments } from './commentsApi';

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
const noContent = () => new Response(null, { status: 204 });

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('commentsApi', () => {
  it('listComments GETs the comments route', async () => {
    const stub = stubFetch(ok([]));
    await listComments('a/b');
    expect(sent(stub)).toEqual({
      url: '/api/work-items/a%2Fb/comments',
      method: 'GET',
      body: undefined,
    });
    expect(commentKeys.list('x')).toEqual(['comments', 'x']);
  });

  it('addComment sends only the body', async () => {
    const stub = stubFetch(ok());
    await addComment('a/b', 'word for word');
    expect(sent(stub)).toEqual({
      url: '/api/work-items/a%2Fb/comments',
      method: 'POST',
      body: { body: 'word for word' },
    });
  });

  it('archiveComment POSTs the archive route with no body', async () => {
    const stub = stubFetch(noContent());
    await expect(archiveComment('c/1')).resolves.toBeUndefined();
    expect(sent(stub)).toEqual({
      url: '/api/comments/c%2F1/archive',
      method: 'POST',
      body: undefined,
    });
  });
});
