import { afterEach, beforeEach, expect, test, vi } from 'vitest';

// A fresh page load per test: the web platform remembers a sign-in redirect it has started. Requests go through the
// core's client, configured with this platform as main.tsx does, so each case is the response the browser sees.
let client: typeof import('@shiplean/core/api/client');
let webPlatform: typeof import('./webPlatform').webPlatform;
let VIEWPORT_HEADER: string;

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
  const seam = await import('@shiplean/core/platform');
  ({ webPlatform, VIEWPORT_HEADER } = await import('./webPlatform'));
  seam.configurePlatform(webPlatform);
  client = await import('@shiplean/core/api/client');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

test('webPlatform_WhenApiErrorsWithoutLosingTheSession_ShouldNotRedirect', async () => {
  stubFetch(apiError(409, 'conflict', 'Someone else changed it.'));

  const error = await client.api.get('/api/things').catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 409, code: 'conflict' });
  expect(assign).not.toHaveBeenCalled();
});

test('webPlatform_When401_ShouldRedirectToLogin', async () => {
  stubFetch(apiError(401, 'unauthorized', 'Sign in.'));

  const error = await client.api.get('/api/things').catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 401, code: 'unauthorized' });
  expect(assign).toHaveBeenCalledWith(
    '/login?returnUrl=' + encodeURIComponent('/p/SHIP/sprints?x=1'),
  );
});

test('webPlatform_When401OnLogin_ShouldNotRedirect', async () => {
  stubLocation('/login', '?returnUrl=%2F');
  stubFetch(apiError(401, 'unauthorized', 'Wrong email or password.'));

  const error = await client.api.post('/api/auth/login', {}).catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 401, message: 'Wrong email or password.' });
  expect(assign).not.toHaveBeenCalled();
});

test.each([['/LOGIN'], ['/login/'], ['/login//']])(
  'webPlatform_When401OnAnotherSpellingOfTheSignInPage_ShouldNotRedirect (%s)',
  async (pathname) => {
    stubLocation(pathname);
    stubFetch(apiError(401, 'unauthorized', 'Wrong email or password.'));

    const error = await client.api.post('/api/auth/login', {}).catch((e: unknown) => e);

    expect(error).toMatchObject({ status: 401, message: 'Wrong email or password.' });
    expect(assign).not.toHaveBeenCalled();
  },
);

test('webPlatform_WhenSessionEnded_ShouldOpenLoginWithState', async () => {
  stubFetch(apiError(401, 'session_ended', 'Your session ended. Sign in again.'));

  const error = await client.api.get('/api/things').catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 401, code: 'session_ended' });
  expect(assign).toHaveBeenCalledWith(STATE_ENDED);
});

test('webPlatform_WhenPasswordChangeRequired_ShouldOpenLoginWithState', async () => {
  stubFetch(apiError(403, 'password_change_required', 'Choose your own password first.'));

  const error = await client.api.get('/api/things').catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 403, code: 'password_change_required' });
  expect(assign).toHaveBeenCalledWith(STATE_CHANGE);
});

test('webPlatform_WhenPlain401sFollowSessionEnded_ShouldKeepTheEndedRedirect', async () => {
  // The session_ended answer deletes the cookie, so requests sent after it answer a plain 401 before the page has left.
  stubFetch(apiError(401, 'session_ended', 'Your session ended. Sign in again.'));
  await client.api.get('/api/me').catch(() => undefined);
  stubFetch(apiError(401, 'unauthorized', 'Sign in to continue.'));
  await client.api.get('/api/projects').catch(() => undefined);

  expect(assign).toHaveBeenCalledTimes(1);
  expect(assign).toHaveBeenCalledWith(STATE_ENDED);
});

test('webPlatform_WhenSessionEndedFollowsAPlain401_ShouldOpenLoginWithState', async () => {
  stubFetch(apiError(401, 'unauthorized', 'Sign in to continue.'));
  await client.api.get('/api/projects').catch(() => undefined);
  stubFetch(apiError(401, 'session_ended', 'Your session ended. Sign in again.'));
  await client.api.get('/api/me').catch(() => undefined);

  expect(assign).toHaveBeenLastCalledWith(STATE_ENDED);
  stubFetch(apiError(401, 'session_ended', 'Your session ended. Sign in again.'));
  await client.api.get('/api/me').catch(() => undefined);
  expect(assign).toHaveBeenCalledTimes(2);
});

test('webPlatform_ShouldSendSameOriginCookieRequestsWithNoAuthHeaders', async () => {
  const fetchStub = stubFetch(new Response(JSON.stringify({ ok: true }), { status: 200 }));
  vi.stubGlobal('innerWidth', 1280);

  await client.api.get('/api/things');

  const [url, init] = fetchStub.mock.calls[0] as [string, RequestInit];
  expect(url).toBe('/api/things');
  expect(init.credentials).toBe('same-origin');
  expect(init.headers).toEqual({
    [client.CLIENT_HEADER]: client.clientId,
    [VIEWPORT_HEADER]: 'desktop',
  });
});

test('webPlatform_ShouldUseTheBrowsersStorage', () => {
  webPlatform.persistentStore().setItem('web-platform-test', 'kept');
  webPlatform.sessionStore().setItem('web-platform-test', 'tab');

  expect(localStorage.getItem('web-platform-test')).toBe('kept');
  expect(sessionStorage.getItem('web-platform-test')).toBe('tab');

  webPlatform.persistentStore().removeItem('web-platform-test');
  webPlatform.sessionStore().removeItem('web-platform-test');
  expect(webPlatform.persistentStore().getItem('web-platform-test')).toBeNull();
  expect(webPlatform.sessionStore().getItem('web-platform-test')).toBeNull();
});

test('webPlatform_ShouldFollowTheBrowsersConnection', () => {
  const online = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
  expect(webPlatform.isOffline()).toBe(true);
  online.mockReturnValue(true);
  expect(webPlatform.isOffline()).toBe(false);

  const listener = vi.fn();
  const stop = webPlatform.onOnline(listener);
  window.dispatchEvent(new Event('online'));
  expect(listener).toHaveBeenCalledTimes(1);
  stop();
  window.dispatchEvent(new Event('online'));
  expect(listener).toHaveBeenCalledTimes(1);
  online.mockRestore();
});

test('webPlatform_ShouldSendTheViewportHeader', async () => {
  const fetchStub = vi.fn(() => Promise.resolve(new Response('{}', { status: 200 })));
  vi.stubGlobal('fetch', fetchStub);

  vi.stubGlobal('innerWidth', 390);
  await client.api.get('/api/things');
  // Read at send time: a resize between two requests changes the second one's header.
  vi.stubGlobal('innerWidth', 1280);
  await client.api.post('/api/things', { name: 'x' });

  const headersOf = (call: number) =>
    (fetchStub.mock.calls[call] as unknown as [string, RequestInit])[1].headers;
  expect(VIEWPORT_HEADER).toBe('X-ShipLean-Viewport');
  expect(headersOf(0)).toMatchObject({ [VIEWPORT_HEADER]: 'phone' });
  expect(headersOf(1)).toMatchObject({ [VIEWPORT_HEADER]: 'desktop' });
});

test('webPlatform_WhenSendingAForm_ShouldSendTheViewportHeader', async () => {
  const fetchStub = stubFetch(new Response(JSON.stringify({ ok: true }), { status: 200 }));
  vi.stubGlobal('innerWidth', 700);

  await client.sendForm('POST', '/api/files', new FormData(), 'Too large.');

  const [, init] = fetchStub.mock.calls[0] as [string, RequestInit];
  expect(init.headers).toMatchObject({
    [client.CLIENT_HEADER]: client.clientId,
    [VIEWPORT_HEADER]: 'tablet',
  });
});
