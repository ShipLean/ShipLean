// A fake server at `fetch`: the real core client runs, and only the network is fake. Runner-neutral (no jest here),
// so pure vitest tests can use it too.
//
// The table maps "METHOD /path" (no origin, no query) to one of:
//   - a body, answered 200 as JSON;
//   - { status, body? }, answered with that status (a 204 needs no body);
//   - a function of the request returning either of those, or a promise of one.
// An unlisted request answers 404 not_found. Every request is recorded; read them with callsTo('GET /api/me').

export interface FakeRequest {
  method: string;
  // The URL without its origin or query: '/api/me'.
  path: string;
  // Everything before the path: 'https://shiplean.test', or '' for a relative URL.
  origin: string;
  url: string;
  headers: Record<string, string>;
  // The parsed JSON body, or undefined when there is none.
  body: unknown;
}

export interface ApiReply {
  status: number;
  body?: unknown;
}

export type ApiAnswer = unknown;
export type ApiHandler = (request: FakeRequest) => ApiAnswer | Promise<ApiAnswer>;
export type ApiTable = Record<string, ApiAnswer | ApiHandler>;

export interface FakeApi {
  readonly calls: readonly FakeRequest[];
  callsTo(route: string): FakeRequest[];
  // fakeApi() installs itself; renderApp installs the api it is given again, so the last one rendered answers.
  install(): void;
}

const realFetch = globalThis.fetch;

// The error body every endpoint returns (core: ApiError).
export function apiError(status: number, code: string, error = `Failed (${code}).`): ApiReply {
  return { status, body: { error, code } };
}

export const sessionEnded = (): ApiReply =>
  apiError(401, 'session_ended', 'Your session has ended. Sign in again.');

function isReply(answer: unknown): answer is ApiReply {
  if (typeof answer !== 'object' || answer === null || Array.isArray(answer)) return false;
  const keys = Object.keys(answer);
  return (
    typeof (answer as { status?: unknown }).status === 'number' &&
    keys.every((k) => k === 'status' || k === 'body')
  );
}

function headersOf(init: RequestInit | undefined): Record<string, string> {
  const headers: Record<string, string> = {};
  const raw = init?.headers;
  if (raw === undefined) return headers;
  if (Array.isArray(raw)) {
    for (const [k, v] of raw) headers[k] = v;
  } else if (typeof (raw as Headers).forEach === 'function') {
    (raw as Headers).forEach((v, k) => {
      headers[k] = v;
    });
  } else {
    Object.assign(headers, raw);
  }
  return headers;
}

function parseBody(body: RequestInit['body']): unknown {
  if (typeof body !== 'string') return body ?? undefined;
  try {
    return JSON.parse(body);
  } catch {
    return body;
  }
}

// Enough of a Response for the core client: ok, status, json() and text().
function response(status: number, body: unknown): Response {
  const text = body === undefined ? '' : JSON.stringify(body);
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: {
      get: (name: string) => (name.toLowerCase() === 'content-type' ? 'application/json' : null),
    },
    json: async () => JSON.parse(text),
    text: async () => text,
  } as unknown as Response;
}

export function fakeApi(table: ApiTable): FakeApi {
  const calls: FakeRequest[] = [];

  const fetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    const origin = /^[a-z][a-z0-9+.-]*:\/\/[^/?#]*/i.exec(url)?.[0] ?? '';
    const path = url.slice(origin.length).split(/[?#]/)[0] || '/';
    const request: FakeRequest = {
      method: (init?.method ?? 'GET').toUpperCase(),
      path,
      origin,
      url,
      headers: headersOf(init),
      body: parseBody(init?.body),
    };
    calls.push(request);
    if (init?.signal?.aborted) throw new DOMException('The operation was aborted.', 'AbortError');

    const route = `${request.method} ${path}`;
    if (!(route in table)) {
      const missing = apiError(404, 'not_found', `No fake answer for ${route}.`);
      return response(missing.status, missing.body);
    }
    const entry = table[route];
    const answer = typeof entry === 'function' ? await (entry as ApiHandler)(request) : entry;
    return isReply(answer) ? response(answer.status, answer.body) : response(200, answer);
  };

  const api: FakeApi = {
    calls,
    callsTo: (route) => calls.filter((c) => `${c.method} ${c.path}` === route),
    install: () => {
      globalThis.fetch = fetch as typeof globalThis.fetch;
    },
  };
  api.install();
  return api;
}

// Puts the real fetch back; the jest setup runs it after each spec.
export function restoreFetch(): void {
  globalThis.fetch = realFetch;
}
