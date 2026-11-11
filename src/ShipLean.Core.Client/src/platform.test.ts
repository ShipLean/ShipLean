import { beforeEach, expect, test, vi } from 'vitest';

// A fresh module per test: the platform is module state, and these tests check it before anything configures it.
let seam: typeof import('./platform');

beforeEach(async () => {
  vi.resetModules();
  seam = await import('./platform');
});

test('platform_WhenNotConfigured_ShouldRunInMemoryOnlineOnItsOwnOrigin', () => {
  const p = seam.platform();

  expect(p.isOffline()).toBe(false);
  expect(p.apiOrigin()).toBe('');
  expect(p.authHeaders()).toEqual({});
  expect(p.credentials()).toBe('same-origin');
  expect(() => p.onUnauthorized('session_ended')).not.toThrow();
  const listener = vi.fn();
  const stop = p.onOnline(listener);
  expect(() => stop()).not.toThrow();
  expect(listener).not.toHaveBeenCalled();

  p.persistentStore().setItem('k', 'kept');
  p.sessionStore().setItem('k', 'tab');
  expect(seam.platform().persistentStore().getItem('k')).toBe('kept');
  expect(seam.platform().sessionStore().getItem('k')).toBe('tab');
});

test('platform_WhenConfigured_ShouldAnswerWithTheHost', () => {
  const host = { ...seam.platform(), apiOrigin: () => 'https://ship.example.com' };

  seam.configurePlatform(host);

  expect(seam.platform()).toBe(host);
  expect(seam.platform().apiOrigin()).toBe('https://ship.example.com');
});

test('memoryStore_ShouldSetGetAndRemove', () => {
  const store = seam.memoryStore();

  expect(store.getItem('missing')).toBeNull();
  store.setItem('a', '1');
  store.setItem('a', '2');
  expect(store.getItem('a')).toBe('2');
  store.removeItem('a');
  expect(store.getItem('a')).toBeNull();
  expect(seam.memoryStore().getItem('a')).toBeNull();
});
