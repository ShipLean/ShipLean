import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import errors from '../../../ShipLean.TestSupport/DtoFixtures/errors/api-error.json';

// A fresh module per test, as each page load gets: the client remembers a sign-in redirect it has started.
let client: typeof import('./client');

const assign = vi.fn();
const HERE = encodeURIComponent('/p/SHIP/sprints?x=1');
const STATE_ENDED = `/login?state=ended&returnUrl=${HERE}`;
const STATE_CHANGE = `/login?state=change&returnUrl=${HERE}`;

function stubLocation(pathname: string, search = '') {
  vi.stubGlobal('location', { pathname, search, assign });
}

function stubFetch(response: Response) {
  const fetchStub = vi.fn().mockResolvedValue(response);
  vi.stubGlobal('fetch', fetchStub);
  return fetchStub;
}

const apiError = (status: number, code: string, error: string) =>
  new Response(JSON.stringify({ error, code }), { status });

beforeEach(async () => {
  assign.mockReset();
  stubLocation('/p/SHIP/sprints', '?x=1');
  vi.resetModules();
  client = await import('./client');
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
  expect(assign).not.toHaveBeenCalled();
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

test('client_When401_ShouldRedirectToLogin', async () => {
  stubFetch(apiError(401, 'unauthorized', 'Sign in.'));

  const error = await client.api.get('/api/things').catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 401, code: 'unauthorized' });
  expect(assign).toHaveBeenCalledWith(
    '/login?returnUrl=' + encodeURIComponent('/p/SHIP/sprints?x=1'),
  );
});

test('client_When401OnLogin_ShouldNotRedirect', async () => {
  stubLocation('/login', '?returnUrl=%2F');
  stubFetch(apiError(401, 'unauthorized', 'Wrong email or password.'));

  const error = await client.api.post('/api/auth/login', {}).catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 401, message: 'Wrong email or password.' });
  expect(assign).not.toHaveBeenCalled();
});

test.each([['/LOGIN'], ['/login/'], ['/login//']])(
  'client_When401OnAnotherSpellingOfTheSignInPage_ShouldNotRedirect (%s)',
  async (pathname) => {
    stubLocation(pathname);
    stubFetch(apiError(401, 'unauthorized', 'Wrong email or password.'));

    const error = await client.api.post('/api/auth/login', {}).catch((e: unknown) => e);

    expect(error).toMatchObject({ status: 401, message: 'Wrong email or password.' });
    expect(assign).not.toHaveBeenCalled();
  },
);

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

test('client_WhenSessionEnded_ShouldOpenLoginWithState', async () => {
  stubFetch(apiError(401, 'session_ended', 'Your session ended. Sign in again.'));

  const error = await client.api.get('/api/things').catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 401, code: 'session_ended' });
  expect(assign).toHaveBeenCalledWith(STATE_ENDED);
});

test('client_WhenPasswordChangeRequired_ShouldOpenLoginWithState', async () => {
  stubFetch(apiError(403, 'password_change_required', 'Choose your own password first.'));

  const error = await client.api.get('/api/things').catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 403, code: 'password_change_required' });
  expect(assign).toHaveBeenCalledWith(STATE_CHANGE);
});

test('client_WhenPlain401sFollowSessionEnded_ShouldKeepTheEndedRedirect', async () => {
  // The session_ended answer deletes the cookie, so requests sent after it answer a plain 401 before the page has left.
  stubFetch(apiError(401, 'session_ended', 'Your session ended. Sign in again.'));
  await client.api.get('/api/me').catch(() => undefined);
  stubFetch(apiError(401, 'unauthorized', 'Sign in to continue.'));
  await client.api.get('/api/projects').catch(() => undefined);

  expect(assign).toHaveBeenCalledTimes(1);
  expect(assign).toHaveBeenCalledWith(STATE_ENDED);
});

test('client_WhenSessionEndedFollowsAPlain401_ShouldOpenLoginWithState', async () => {
  stubFetch(apiError(401, 'unauthorized', 'Sign in to continue.'));
  await client.api.get('/api/projects').catch(() => undefined);
  stubFetch(apiError(401, 'session_ended', 'Your session ended. Sign in again.'));
  await client.api.get('/api/me').catch(() => undefined);

  expect(assign).toHaveBeenLastCalledWith(STATE_ENDED);
  stubFetch(apiError(401, 'session_ended', 'Your session ended. Sign in again.'));
  await client.api.get('/api/me').catch(() => undefined);
  expect(assign).toHaveBeenCalledTimes(2);
});
