// The fetch stub the api tests share: stub the global fetch, then read back what the client sent.
import { vi } from 'vitest';

export function stubFetch(response: Response) {
  const fetchStub = vi.fn().mockResolvedValue(response);
  vi.stubGlobal('fetch', fetchStub);
  return fetchStub;
}

export function sent(fetchStub: ReturnType<typeof stubFetch>) {
  const [url, init] = fetchStub.mock.calls[0] as [string, RequestInit];
  return {
    url,
    method: init.method,
    body: init.body === undefined ? undefined : (JSON.parse(String(init.body)) as unknown),
  };
}

export const ok = (body: unknown = {}, status = 200) =>
  new Response(JSON.stringify(body), { status });

export const noContent = () => new Response(null, { status: 204 });
