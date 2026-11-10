// fakeApi with controls for the runner's failure paths. Runner-neutral (no jest), so pure vitest tests can use it.
//   failNext(route, reply): the next request to the route gets this reply, once;
//   offline(true): every request rejects with a TypeError, as fetch does with no connection (and isn't recorded);
//   hang(route): requests to the route wait until release(route), then get their usual answer.
import {
  apiError,
  fakeApi,
  type ApiAnswer,
  type ApiHandler,
  type ApiReply,
  type ApiTable,
  type FakeApi,
} from './fakeApi';

export interface ControlledApi extends FakeApi {
  failNext(route: string, reply: ApiReply): void;
  offline(on: boolean): void;
  hang(route: string): void;
  release(route: string): void;
}

export function controlledApi(table: ApiTable): ControlledApi {
  const failures = new Map<string, ApiReply[]>();
  const hanging = new Set<string>();
  const waiting = new Map<string, (() => void)[]>();
  let isOffline = false;

  const controlled =
    (route: string, original: ApiHandler | ApiAnswer | undefined): ApiHandler =>
    async (request) => {
      const failure = failures.get(route)?.shift();
      if (failure !== undefined) return failure;
      if (hanging.has(route)) {
        await new Promise<void>((resolve) => {
          waiting.set(route, [...(waiting.get(route) ?? []), resolve]);
        });
      }
      if (original === undefined) return apiError(404, 'not_found', `No fake answer for ${route}.`);
      return typeof original === 'function' ? (original as ApiHandler)(request) : original;
    };

  // fakeApi reads the table at request time, so a control added later for an unlisted route is seen.
  const wrapped: ApiTable = {};
  for (const [route, entry] of Object.entries(table)) wrapped[route] = controlled(route, entry);
  const ensure = (route: string) => {
    if (!(route in wrapped)) wrapped[route] = controlled(route, undefined);
  };

  const base = fakeApi(wrapped);
  const innerFetch = globalThis.fetch;
  const fetch = (input: RequestInfo | URL, init?: RequestInit): Promise<Response> =>
    isOffline ? Promise.reject(new TypeError('Network request failed')) : innerFetch(input, init);
  const install = () => {
    globalThis.fetch = fetch as typeof globalThis.fetch;
  };
  install();

  return {
    calls: base.calls,
    callsTo: base.callsTo,
    install,
    failNext: (route, reply) => {
      ensure(route);
      failures.set(route, [...(failures.get(route) ?? []), reply]);
    },
    offline: (on) => {
      isOffline = on;
    },
    hang: (route) => {
      ensure(route);
      hanging.add(route);
    },
    release: (route) => {
      hanging.delete(route);
      const resolvers = waiting.get(route) ?? [];
      waiting.delete(route);
      for (const resolve of resolvers) resolve();
    },
  };
}
