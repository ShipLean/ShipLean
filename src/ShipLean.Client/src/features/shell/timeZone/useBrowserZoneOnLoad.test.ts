import { afterEach, describe, expect, test, vi } from 'vitest';

// The hook keeps its once-per-page-load flag at module level, so each test loads the whole module graph (React and the query
// library with it) again, like a fresh page load. The browser zone is a stub the test picks.
async function freshPage(zone: () => string | null) {
  vi.resetModules();
  vi.doMock('./browserZone', () => ({ browserZone: zone }));
  const [{ QueryClient, QueryClientProvider }, { act, createElement }, { createRoot }, hook] =
    await Promise.all([
      import('@tanstack/react-query'),
      import('react'),
      import('react-dom/client'),
      import('./useBrowserZoneOnLoad'),
    ]);
  const roots: { unmount(): void }[] = [];
  function Probe() {
    hook.useBrowserZoneOnLoad();
    return null;
  }
  // One shell mount under its own query client, with another screen's query already cached; the returned function unmounts it.
  function mount() {
    Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    client.setQueryData(OTHER_KEY, 'labels in UTC');
    const root = createRoot(document.createElement('div'));
    roots.push(root);
    act(() => root.render(createElement(QueryClientProvider, { client }, createElement(Probe))));
    return { client, unmount: () => act(() => root.unmount()) };
  }
  return { mount, act, cleanup: () => roots.forEach((root) => act(() => root.unmount())) };
}

const OTHER_KEY = ['other-screen'] as const;

const reply = (body: unknown) =>
  Promise.resolve(
    new Response(JSON.stringify(body), {
      status: 200,
      headers: { 'content-type': 'application/json' },
    }),
  );

// The server: GET /api/me answers the given zone every time; PUT /api/me/time-zone answers the stored zone.
function stubServer(zone: string | null) {
  const sent: { method: string; path: string; body?: unknown }[] = [];
  vi.stubGlobal('fetch', (url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET';
    const path = new URL(url, 'http://localhost').pathname;
    sent.push({ method, path, body: init?.body ? JSON.parse(String(init.body)) : undefined });
    if (path === '/api/me/time-zone') return reply({ id: 'me', timeZone: 'Asia/Bangkok' });
    return reply({ id: 'me', timeZone: zone, timeZoneLabel: 'Not set: times show in UTC' });
  });
  return sent;
}

let cleanup: (() => void) | undefined;
afterEach(() => {
  cleanup?.();
  cleanup = undefined;
  vi.unstubAllGlobals();
  vi.doUnmock('./browserZone');
});

async function page(zone: () => string | null = () => 'Asia/Bangkok') {
  const loaded = await freshPage(zone);
  cleanup = loaded.cleanup;
  return loaded;
}

const puts = (sent: { method: string }[]) => sent.filter((s) => s.method === 'PUT');
const meReads = (sent: { method: string; path: string }[]) =>
  sent.filter((s) => s.method === 'GET' && s.path === '/api/me').length;

describe('useBrowserZoneOnLoad', () => {
  test('useBrowserZoneOnLoad_WhenUserHasNoZone_ShouldSendTheBrowsersZoneOnce', async () => {
    const sent = stubServer(null);
    const { mount } = await page();
    mount();

    // The save re-reads ['me'], which still answers no zone here: the hook must not send again.
    await vi.waitFor(() => expect(meReads(sent)).toBeGreaterThanOrEqual(2));
    expect(puts(sent)).toEqual([
      {
        method: 'PUT',
        path: '/api/me/time-zone',
        body: { timeZone: 'Asia/Bangkok', source: 'browser' },
      },
    ]);
  });

  // Mutation: drop the module-level once flag (use a per-mount ref instead).
  test('useBrowserZoneOnLoad_WhenShellMountsAgain_ShouldNotSendAgain', async () => {
    const sent = stubServer(null);
    const { mount } = await page();
    const first = mount();
    await vi.waitFor(() => expect(puts(sent)).toHaveLength(1));
    first.unmount();

    mount();

    await vi.waitFor(() => expect(meReads(sent)).toBeGreaterThanOrEqual(3));
    await Promise.resolve();
    expect(puts(sent)).toHaveLength(1);
  });

  test('useBrowserZoneOnLoad_WhenUserHasAZone_ShouldSendNothingAndAskNoZone', async () => {
    const sent = stubServer('Europe/London');
    const zone = vi.fn(() => 'Asia/Bangkok');
    const { mount, act } = await page(zone);
    mount();

    await vi.waitFor(() => expect(meReads(sent)).toBe(1));
    await act(() => Promise.resolve());
    expect(puts(sent)).toEqual([]);
    expect(zone).not.toHaveBeenCalled();
  });

  test('useBrowserZoneOnLoad_WhenBrowserGivesNoZone_ShouldSendNothingAndNotRetry', async () => {
    const sent = stubServer(null);
    const zone = vi.fn<() => string | null>(() => null);
    const { mount, act } = await page(zone);
    const first = mount();
    await vi.waitFor(() => expect(zone).toHaveBeenCalledTimes(1));
    first.unmount();
    zone.mockReturnValue('Asia/Bangkok');

    mount();

    await vi.waitFor(() => expect(meReads(sent)).toBe(2));
    await act(() => Promise.resolve());
    await act(() => Promise.resolve());
    expect(zone).toHaveBeenCalledTimes(1);
    expect(puts(sent)).toEqual([]);
  });

  test('useBrowserZoneOnLoad_WhenSaved_ShouldInvalidateEveryQuery', async () => {
    const sent = stubServer(null);
    const { mount } = await page();
    const { client } = mount();

    await vi.waitFor(() => expect(puts(sent)).toHaveLength(1));
    await vi.waitFor(() => expect(client.getQueryState(OTHER_KEY)?.isInvalidated).toBe(true));
  });
});
