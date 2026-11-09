import { describe, expect, test } from 'vitest';
import { checkLine, checkServer } from './serverCheck';

function reply(status: number, body: unknown): typeof fetch {
  return (async () =>
    ({
      ok: status >= 200 && status < 300,
      status,
      json: async () => {
        if (body === undefined) throw new SyntaxError('no json');
        return body;
      },
    }) as Response) as typeof fetch;
}

describe('checkServer', () => {
  test('ok for status ok and a string version', async () => {
    const result = await checkServer('https://s.test', {
      fetch: reply(200, { status: 'ok', version: '1.4.0+abc', search: null }),
    });
    expect(result).toEqual({ state: 'ok', version: '1.4.0+abc' });
  });

  test('asks the health route anonymously', async () => {
    let seen: [unknown, RequestInit | undefined] | undefined;
    await checkServer('https://s.test', {
      fetch: (async (url: unknown, init?: RequestInit) => {
        seen = [url, init];
        return { ok: true, json: async () => ({}) } as Response;
      }) as typeof fetch,
    });
    expect(seen?.[0]).toBe('https://s.test/api/health');
    expect(seen?.[1]?.credentials).toBe('omit');
    expect(seen?.[1]?.headers).toBeUndefined();
  });

  test.each([
    ['another status', { status: 'degraded', version: '1' }],
    ['a numeric version', { status: 'ok', version: 1 }],
    ['no version', { status: 'ok' }],
    ['html', undefined],
    ['null', null],
    ['an array', []],
  ])('a 200 with %s is not a ShipLean server', async (_name, body) => {
    expect(await checkServer('https://s.test', { fetch: reply(200, body) })).toEqual({
      state: 'notShipLean',
    });
  });

  test('an error status is unreachable', async () => {
    expect(
      await checkServer('https://s.test', { fetch: reply(502, { status: 'ok', version: '1' }) }),
    ).toEqual({ state: 'unreachable' });
  });

  test('no answer is unreachable', async () => {
    const fetch = (async () => {
      throw new TypeError('Network request failed');
    }) as typeof globalThis.fetch;
    expect(await checkServer('https://s.test', { fetch })).toEqual({ state: 'unreachable' });
  });

  test('a superseded check is aborted through its signal', async () => {
    const controller = new AbortController();
    let received: AbortSignal | null | undefined;
    const fetch = ((_url: unknown, init?: RequestInit) => {
      received = init?.signal;
      return new Promise((_resolve, reject) => {
        init?.signal?.addEventListener('abort', () => reject(new DOMException('x', 'AbortError')));
      });
    }) as typeof globalThis.fetch;
    const pending = checkServer('https://s.test', { fetch, signal: controller.signal });
    controller.abort();

    expect(await pending).toEqual({ state: 'aborted' });
    expect(received).toBe(controller.signal);
  });
});

describe('checkLine', () => {
  test('the four lines', () => {
    expect(checkLine({ state: 'ok', version: '1.4.0+7f26ef0a' }, 'h')).toBe(
      'ShipLean 1.4.0 · connected',
    );
    expect(checkLine({ state: 'ok', version: '2.0' }, 'h')).toBe('ShipLean 2.0 · connected');
    expect(checkLine({ state: 'notShipLean' }, 'h.test')).toBe(
      "h.test isn't a ShipLean server. Check the address.",
    );
    expect(checkLine({ state: 'unreachable' }, 'h.test')).toBe(
      "Can't reach h.test. Check the address and that the server is running.",
    );
    expect(checkLine({ state: 'aborted' }, 'h')).toBeNull();
  });
});
