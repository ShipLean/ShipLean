import { afterEach, beforeEach, expect, test, vi } from 'vitest';

// A fresh module per test: the client remembers a sign-in redirect it has started, as a page load does.
let settings: typeof import('./settingsApi');

const assign = vi.fn();
const file = new File(['png'], 'me.png', { type: 'image/png' });
const apiError = (status: number, code: string, error: string) =>
  new Response(JSON.stringify({ error, code }), { status });

beforeEach(async () => {
  assign.mockReset();
  vi.stubGlobal('location', { pathname: '/settings/profile', search: '', assign });
  vi.resetModules();
  settings = await import('./settingsApi');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

test('uploadPhoto_WhenServerRefuses_ShouldThrowItsMessageAndStay', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(apiError(400, 'validation', 'Not an image.')));

  const error = await settings.uploadPhoto(file).catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 400, code: 'validation', message: 'Not an image.' });
  expect(assign).not.toHaveBeenCalled();
});

test('uploadPhoto_When401_ShouldSendToSignIn', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(apiError(401, 'unauthorized', 'Sign in.')));

  const error = await settings.uploadPhoto(file).catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 401 });
  expect(assign).toHaveBeenCalledWith(
    '/login?returnUrl=' + encodeURIComponent('/settings/profile'),
  );
});

test('uploadPhoto_WhenSessionEnded_ShouldSendToSignInInThatState', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(apiError(401, 'session_ended', 'Ended.')));

  await settings.uploadPhoto(file).catch((e: unknown) => e);

  expect(assign).toHaveBeenCalledWith(
    '/login?state=ended&returnUrl=' + encodeURIComponent('/settings/profile'),
  );
});

test('uploadPhoto_WhenPasswordChangeRequired_ShouldSendToSignInInThatState', async () => {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue(apiError(401, 'password_change_required', 'Change it.')),
  );

  await settings.uploadPhoto(file).catch((e: unknown) => e);

  expect(assign).toHaveBeenCalledWith(
    '/login?state=change&returnUrl=' + encodeURIComponent('/settings/profile'),
  );
});

test('uploadPhoto_WhenBodyOverTheRequestCap_ShouldSayThePhotoIsTooLarge', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 413 })));

  const error = await settings.uploadPhoto(file).catch((e: unknown) => e);

  expect(error).toMatchObject({
    status: 413,
    message: 'That photo is over 2 MB. Pick a smaller one',
  });
  expect(assign).not.toHaveBeenCalled();
});

test('uploadPhoto_ShouldPutTheFileAsMultipartWithTheClientHeader', async () => {
  const fetchStub = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'me' })));
  vi.stubGlobal('fetch', fetchStub);

  await settings.uploadPhoto(file);

  const [path, init] = fetchStub.mock.calls[0] as [string, RequestInit];
  expect(path).toBe('/api/me/photo');
  expect(init.method).toBe('PUT');
  expect((init.body as FormData).get('file')).toBeInstanceOf(File);
  expect(init.headers).toHaveProperty('X-ShipLean-Client');
});
