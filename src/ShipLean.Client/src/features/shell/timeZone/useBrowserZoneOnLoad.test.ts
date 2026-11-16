import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, createElement } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { browserZone } from './browserZone';
import { useBrowserZoneOnLoad } from './useBrowserZoneOnLoad';

vi.mock('./browserZone', () => ({ browserZone: vi.fn(() => 'Asia/Bangkok') }));

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

const roots: Root[] = [];
afterEach(() => {
  roots.splice(0).forEach((root) => act(() => root.unmount()));
  vi.unstubAllGlobals();
  vi.mocked(browserZone).mockReturnValue('Asia/Bangkok');
});

// The hook in a bare component under its own query client, with another screen's query already cached.
function mount() {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(OTHER_KEY, 'labels in UTC');
  function Probe() {
    useBrowserZoneOnLoad();
    return null;
  }
  const root = createRoot(document.createElement('div'));
  roots.push(root);
  act(() => root.render(createElement(QueryClientProvider, { client }, createElement(Probe))));
  return client;
}

const puts = (sent: { method: string }[]) => sent.filter((s) => s.method === 'PUT');
const meReads = (sent: { method: string; path: string }[]) =>
  sent.filter((s) => s.method === 'GET' && s.path === '/api/me').length;

describe('useBrowserZoneOnLoad', () => {
  test('useBrowserZoneOnLoad_WhenUserHasNoZone_ShouldSendTheBrowsersZoneOnce', async () => {
    const sent = stubServer(null);
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

  test('useBrowserZoneOnLoad_WhenUserHasAZone_ShouldSendNothing', async () => {
    const sent = stubServer('Europe/London');
    mount();

    await vi.waitFor(() => expect(meReads(sent)).toBe(1));
    await act(() => Promise.resolve());
    expect(puts(sent)).toEqual([]);
  });

  test('useBrowserZoneOnLoad_WhenBrowserGivesNoZone_ShouldSendNothing', async () => {
    vi.mocked(browserZone).mockReturnValue(null);
    const sent = stubServer(null);
    mount();

    await vi.waitFor(() => expect(meReads(sent)).toBe(1));
    await act(() => Promise.resolve());
    expect(puts(sent)).toEqual([]);
  });

  test('useBrowserZoneOnLoad_WhenSaved_ShouldInvalidateEveryQuery', async () => {
    const sent = stubServer(null);
    const client = mount();

    await vi.waitFor(() => expect(puts(sent)).toHaveLength(1));
    await vi.waitFor(() => expect(client.getQueryState(OTHER_KEY)?.isInvalidated).toBe(true));
  });
});
