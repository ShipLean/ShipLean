import { afterEach, beforeEach, expect, test, vi } from 'vitest';

// A fresh module per test: the upload remembers a sign-in redirect it has started, as a page load does.
let upload: typeof import('./attachmentUpload');

const assign = vi.fn();
const file = new File(['hello'], 'notes.txt', { type: 'text/plain' });
const apiError = (status: number, code: string, error: string) =>
  new Response(JSON.stringify({ error, code }), { status });

beforeEach(async () => {
  assign.mockReset();
  vi.stubGlobal('location', { pathname: '/p/SHIP/items/SHIP-1', search: '', assign });
  vi.resetModules();
  upload = await import('./attachmentUpload');
});

afterEach(() => {
  vi.unstubAllGlobals();
});

test('uploadAttachment_WhenServerRefuses_ShouldThrowItsMessage', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(apiError(400, 'validation', 'Too big.')));

  const error = await upload.uploadAttachment('w1', file).catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 400, code: 'validation', message: 'Too big.' });
  expect(assign).not.toHaveBeenCalled();
});

test('uploadAttachment_When401_ShouldSendToSignIn', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(apiError(401, 'unauthorized', 'Sign in.')));

  const error = await upload.uploadAttachment('w1', file).catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 401 });
  expect(assign).toHaveBeenCalledWith(
    '/login?returnUrl=' + encodeURIComponent('/p/SHIP/items/SHIP-1'),
  );
});

test('uploadAttachment_WhenSessionEnded_ShouldSendToSignInInThatState', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(apiError(401, 'session_ended', 'Ended.')));

  await upload.uploadAttachment('w1', file).catch((e: unknown) => e);

  expect(assign).toHaveBeenCalledWith(
    '/login?state=ended&returnUrl=' + encodeURIComponent('/p/SHIP/items/SHIP-1'),
  );
});

test('uploadAttachment_WhenBodyOverTheRequestCap_ShouldSayTheFileIsTooLarge', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: 413 })));

  const error = await upload.uploadAttachment('w1', file).catch((e: unknown) => e);

  expect(error).toMatchObject({ status: 413, message: 'That file is too large to attach.' });
});

test('uploadAttachment_ShouldSendTheFileAsMultipartWithTheClientHeader', async () => {
  const fetchStub = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: 'a1' })));
  vi.stubGlobal('fetch', fetchStub);

  await upload.uploadAttachment('w1', file);

  const [path, init] = fetchStub.mock.calls[0] as [string, RequestInit];
  expect(path).toBe('/api/work-items/w1/attachments');
  expect(init.body).toBeInstanceOf(FormData);
  expect((init.body as FormData).get('file')).toBeInstanceOf(File);
  expect(init.headers).toHaveProperty('X-ShipLean-Client');
});
