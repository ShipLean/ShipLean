import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import errors from '../../../ShipLean.TestSupport/DtoFixtures/errors/api-error.json';
import type { Platform, UnauthorizedReason } from '../platform';

// A fresh module per test, as each page load gets a new client id; the platform module is fresh with it.
let client: typeof import('./client');
let seam: typeof import('../platform');
let reasons: UnauthorizedReason[];

function stubFetch(response: Response) {
  const fetchStub = vi.fn().mockResolvedValue(response);
  vi.stubGlobal('fetch', fetchStub);
  return fetchStub;
}

const apiError = (status: number, code: string, error: string) =>
  new Response(JSON.stringify({ error, code }), { status });

// A host that records what the client tells it, with its own origin, headers and credentials when a test gives them.
function host(overrides: Partial<Platform> = {}): Platform {
  return {
    ...seam.platform(),
    onUnauthorized: (reason) => reasons.push(reason),
    ...overrides,
  };
}

beforeEach(async () => {
  vi.resetModules();
  seam = await import('../platform');
  client = await import('./client');
  reasons = [];
  seam.configurePlatform(host());
});

afterEach(() => {
  vi.unstubAllGlobals();
});

test('client_WhenApiErrors_ShouldThrowApiRequestError', async () => {
  stubFetch(apiError(409, 'conflict', 'Someone else changed it.'));

  const error = await client.api.get('/api/things').catch((e: unknown) => e);

  expect(error).toBeInstanceOf(client.ApiRequestError);
  expect(error).toMatchObject({
    status: 409,
    code: 'conflict',
    message: 'Someone else changed it.',
  });
  expect(reasons).toEqual([]);
});

test('client_WhenErrorCarriesAField_ShouldKeepItOnTheBody', async () => {
  stubFetch(new Response(JSON.stringify(errors.withField), { status: 409 }));

  const error = await client.api.get('/api/things').catch((e: unknown) => e);

  expect(error).toBeInstanceOf(client.ApiRequestError);
  expect(error).toMatchObject({ status: 409, code: 'slug_taken', body: { field: 'slug' } });
});

test('client_WhenErrorHasNoField_ShouldKeepTheBodyWithoutOne', async () => {
  stubFetch(new Response(JSON.stringify(errors.withoutField), { status: 400 }));

  const error = await client.api.get('/api/things').catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 400, code: 'not_markdown' });
  expect(client.isApiError(errors.withoutField)).toBe(true);
  expect(errors.withoutField).not.toHaveProperty('field');
});

test('client_WhenErrorBodyCarriesMore_ShouldKeepTheWholeBody', async () => {
  const body = {
    error: 'Someone else changed it.',
    code: 'conflict',
    current: { id: 'x' },
    theirChanges: [],
  };
  stubFetch(
    new Response(JSON.stringify(body), {
      status: 409,
      headers: { 'Content-Type': 'application/json' },
    }),
  );

  const error = await client.api.post('/api/things').catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 409, code: 'conflict', body });
});

test('client_WhenErrorBodyIsNotJson_ShouldBeInternalError', async () => {
  stubFetch(new Response('<html>bad gateway</html>', { status: 502 }));

  const error = await client.api.get('/api/things').catch((e: unknown) => e);

  expect(error).toBeInstanceOf(client.ApiRequestError);
  expect(error).toMatchObject({ status: 502, code: 'internal_error' });
});

test('client_ShouldSendTheClientHeader', async () => {
  const fetchStub = stubFetch(new Response(JSON.stringify({ ok: true }), { status: 200 }));

  const result = await client.api.post('/api/things', { name: 'x' });

  expect(result).toEqual({ ok: true });
  const [path, init] = fetchStub.mock.calls[0] as [string, RequestInit];
  expect(path).toBe('/api/things');
  expect(init.method).toBe('POST');
  expect(init.credentials).toBe('same-origin');
  expect(init.body).toBe(JSON.stringify({ name: 'x' }));
  expect(init.headers).toMatchObject({
    [client.CLIENT_HEADER]: client.clientId,
    'Content-Type': 'application/json',
  });
  expect(client.clientId).toMatch(/^[0-9a-f-]{36}$/);
});

test('client_WhenSuccessBodyIsNotJson_ShouldBeInternalError', async () => {
  stubFetch(new Response('<html></html>', { status: 200 }));
  const error = await client.api.get('/api/typo').catch((e: unknown) => e);
  expect(error).toBeInstanceOf(client.ApiRequestError);
  expect(error).toMatchObject({ status: 200, code: 'internal_error' });

  stubFetch(new Response(null, { status: 204 }));
  expect(await client.api.delete('/api/things/1')).toBeUndefined();
});

test('client_WhenRandomUuidIsMissing_ShouldBuildAnIdFromRandomValues', () => {
  vi.stubGlobal('crypto', { getRandomValues: (a: Uint8Array) => a.fill(171) });
  expect(client.newClientId()).toBe('ab'.repeat(16));
});

test.each([
  [401, 'unauthorized', 'unauthorized'],
  [401, 'session_ended', 'session_ended'],
  [403, 'password_change_required', 'password_change_required'],
] as const)(
  'client_WhenTheSessionIsLost_ShouldTellTheHostWhy (%s %s)',
  async (status, code, reason) => {
    stubFetch(apiError(status, code, 'Sign in.'));

    const error = await client.api.get('/api/things').catch((e: unknown) => e);

    expect(error).toMatchObject({ status, code });
    expect(reasons).toEqual([reason]);
  },
);

test('client_WhenA401HasNoErrorBody_ShouldTellTheHostUnauthorized', async () => {
  stubFetch(new Response(null, { status: 401 }));

  await client.api.get('/api/things').catch(() => undefined);

  expect(reasons).toEqual(['unauthorized']);
});

test('client_WhenAFormIsRefusedForALostSession_ShouldTellTheHost', async () => {
  stubFetch(apiError(401, 'session_ended', 'Your session ended. Sign in again.'));

  const error = await client
    .sendForm('POST', '/api/things', new FormData(), 'Too large.')
    .catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 401, code: 'session_ended' });
  expect(reasons).toEqual(['session_ended']);
});

test('client_WhenForbiddenForAnotherReason_ShouldNotTellTheHost', async () => {
  stubFetch(apiError(403, 'forbidden', 'Not yours.'));

  await client.api.get('/api/things').catch(() => undefined);

  expect(reasons).toEqual([]);
});

test.each([
  [400, 'validation'],
  [401, 'unauthorized'],
  [401, 'session_ended'],
  [403, 'password_change_required'],
] as const)(
  'client_WhenASignInRequestIsRefused_ShouldNeverTellTheHost (%s %s)',
  async (status, code) => {
    stubFetch(apiError(status, code, 'Sign-in expired. Try again.'));

    const error = await client.api
      .post('/api/auth/device-sessions/exchange', {}, { signIn: true })
      .catch((e: unknown) => e);

    expect(error).toMatchObject({ status, code });
    expect(reasons).toEqual([]);
  },
);

test('client_ShouldSendToTheHostsOriginWithItsHeadersAndCredentials', async () => {
  seam.configurePlatform(
    host({
      apiOrigin: () => 'https://ship.example.com:8443',
      authHeaders: () => ({ Authorization: 'Bearer shl_dev_k3Xq9TbA' }),
      credentials: () => 'omit',
    }),
  );
  const fetchStub = stubFetch(new Response(JSON.stringify({ ok: true }), { status: 200 }));

  await client.api.put('/api/things/1', { name: 'x' });

  const [url, init] = fetchStub.mock.calls[0] as [string, RequestInit];
  expect(url).toBe('https://ship.example.com:8443/api/things/1');
  expect(init.credentials).toBe('omit');
  expect(init.headers).toEqual({
    Authorization: 'Bearer shl_dev_k3Xq9TbA',
    [client.CLIENT_HEADER]: client.clientId,
    'Content-Type': 'application/json',
  });
});

test('client_WhenSendingAForm_ShouldUseTheHostsOriginHeadersAndCredentials', async () => {
  seam.configurePlatform(
    host({
      apiOrigin: () => 'https://ship.example.com',
      authHeaders: () => ({ Authorization: 'Bearer shl_dev_k3Xq9TbA' }),
      credentials: () => 'omit',
    }),
  );
  const fetchStub = stubFetch(new Response(JSON.stringify({ ok: true }), { status: 200 }));

  await client.sendForm('POST', '/api/uploads', new FormData(), 'Too large.');

  const [url, init] = fetchStub.mock.calls[0] as [string, RequestInit];
  expect(url).toBe('https://ship.example.com/api/uploads');
  expect(init.credentials).toBe('omit');
  expect(init.headers).toEqual({
    Authorization: 'Bearer shl_dev_k3Xq9TbA',
    [client.CLIENT_HEADER]: client.clientId,
  });
});
