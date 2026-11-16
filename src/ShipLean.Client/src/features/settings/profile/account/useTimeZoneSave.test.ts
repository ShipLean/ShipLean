import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';
import { act, createElement } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, describe, expect, test, vi } from 'vitest';
import { meKey } from '../../../../queries/shared';
import { useTimeZoneSave } from './useTimeZoneSave';

const BOARD_KEY = ['board-labels'] as const;

const reply = (status: number, body: unknown) =>
  Promise.resolve(
    new Response(JSON.stringify(body), {
      status,
      headers: { 'content-type': 'application/json' },
    }),
  );

// The server: each read of another screen answers the labels in the zone stored at that moment.
function stubServer({ refuse = false } = {}) {
  let zone = 'Asia/Bangkok';
  const sent: unknown[] = [];
  vi.stubGlobal('fetch', (url: string, init?: RequestInit) => {
    const path = new URL(url, 'http://localhost').pathname;
    if (path === '/api/me/time-zone') {
      const body = JSON.parse(String(init?.body)) as { timeZone: string };
      sent.push(body);
      if (refuse) return reply(400, { code: 'validation', error: 'Not a time zone.' });
      zone = body.timeZone;
      return reply(200, { id: 'me', timeZone: zone, timeZoneLabel: zone });
    }
    return reply(200, `labels in ${zone}`);
  });
  return sent;
}

type Save = ReturnType<typeof useTimeZoneSave>;

const roots: Root[] = [];
afterEach(() => {
  roots.splice(0).forEach((root) => act(() => root.unmount()));
  vi.unstubAllGlobals();
});

// The Profile's save hook beside another screen's query, both mounted under one query client.
function mount() {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const result = { save: undefined as unknown as Save, board: undefined as string | undefined };
  function Probe() {
    result.save = useTimeZoneSave();
    result.board = useQuery({
      queryKey: BOARD_KEY,
      queryFn: async () => (await fetch('/api/board')).json() as Promise<string>,
    }).data;
    return null;
  }
  const root = createRoot(document.createElement('div'));
  roots.push(root);
  act(() => root.render(createElement(QueryClientProvider, { client }, createElement(Probe))));
  return { client, result };
}

describe('useTimeZoneSave', () => {
  test('useTimeZoneSave_WhenZoneChanges_ShouldRelabelOtherScreens', async () => {
    const sent = stubServer();
    const { client, result } = mount();
    await vi.waitFor(() => expect(result.board).toBe('labels in Asia/Bangkok'));

    act(() => result.save.save('Europe/London'));

    await vi.waitFor(() => expect(result.board).toBe('labels in Europe/London'));
    expect(sent).toEqual([{ timeZone: 'Europe/London', source: 'profile' }]);
    expect(client.getQueryData<{ timeZone: string }>(meKey)?.timeZone).toBe('Europe/London');
  });

  test('useTimeZoneSave_WhenServerRefuses_ShouldGiveItsMessageAndKeepTheLabels', async () => {
    stubServer({ refuse: true });
    const { result } = mount();
    await vi.waitFor(() => expect(result.board).toBe('labels in Asia/Bangkok'));

    act(() => result.save.save('Mars/Olympus'));

    await vi.waitFor(() => expect(result.save.refusal).toBe('Not a time zone.'));
    expect(result.board).toBe('labels in Asia/Bangkok');
  });
});
