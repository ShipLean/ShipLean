import { afterEach, describe, expect, it, vi } from 'vitest';
import { getMembers, getSprints, getStates, projectMetaKeys } from './projectMeta';

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

describe('projectMeta', () => {
  it.each([
    ['getStates', getStates, 'states'],
    ['getMembers', getMembers, 'members'],
    ['getSprints', getSprints, 'sprints'],
  ] as const)('%s GETs the project %s route', async (_name, fetcher, segment) => {
    const stub = stubFetch(ok([]));
    await fetcher('p/1');
    expect(sent(stub)).toEqual({
      url: `/api/projects/p%2F1/${segment}`,
      method: 'GET',
      body: undefined,
    });
    expect(projectMetaKeys[segment]('p')).toEqual(['projectMeta', 'p', segment]);
  });
});
