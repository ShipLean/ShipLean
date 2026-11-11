import { memoryStore, type KeyValueStore } from '@shiplean/core/platform';
import { describe, expect, test } from 'vitest';
import { createConnectivity, createMobilePlatform } from '../mobilePlatformCore';
import {
  DEFAULT_ORIGIN,
  EMAIL_KEY,
  SECRET_KEY,
  SERVER_KEY,
  createSessionStore,
  type SecureStore,
} from './sessionStore';

function secureStore(initial: Record<string, string> = {}) {
  const values = new Map(Object.entries(initial));
  const store: SecureStore & { values: Map<string, string> } = {
    values,
    getItem: (k) => values.get(k) ?? null,
    setItem: (k, v) => {
      values.set(k, v);
    },
    deleteItemAsync: async (k) => {
      values.delete(k);
    },
  };
  return store;
}

function kvWith(initial: Record<string, string> = {}): KeyValueStore {
  const kv = memoryStore();
  for (const [k, v] of Object.entries(initial)) kv.setItem(k, v);
  return kv;
}

// The stored format: the secret bound to its server.
function bound(origin: string, secret: string): string {
  return JSON.stringify({ origin, secret });
}

function signedInStores() {
  return {
    secure: secureStore({ [SECRET_KEY]: bound('https://a.test', 'shl_dev_old') }),
    kv: kvWith({ [SERVER_KEY]: 'https://a.test', [EMAIL_KEY]: 'a@a.test' }),
  };
}

describe('createSessionStore', () => {
  test('get_WhenStorageHoldsASession_ShouldBeSignedInOnTheFirstSynchronousRead', () => {
    const store = createSessionStore({ ...signedInStores(), defaultOrigin: DEFAULT_ORIGIN });

    expect(store.get()).toEqual({
      origin: 'https://a.test',
      email: 'a@a.test',
      secret: 'shl_dev_old',
      signedIn: true,
      pending: null,
    });
  });

  test('get_WhenNothingIsStored_ShouldBeSignedOutOnTheDefaultOrigin', () => {
    const store = createSessionStore({
      secure: secureStore(),
      kv: kvWith(),
      defaultOrigin: DEFAULT_ORIGIN,
    });

    expect(store.get()).toEqual({
      origin: 'https://prod.shiplean.app',
      email: '',
      secret: null,
      signedIn: false,
      pending: null,
    });
  });

  test('get_WhenNoDefaultOriginAndNothingStored_ShouldHaveNoOrigin', () => {
    const store = createSessionStore({ secure: secureStore(), kv: kvWith() });

    expect(store.get().origin).toBeNull();
  });

  test('commit_WhenSignedIn_ShouldPutTheSecretOnlyInTheSecureStore', () => {
    const secure = secureStore();
    const kv = kvWith();
    const store = createSessionStore({ secure, kv, defaultOrigin: DEFAULT_ORIGIN });

    store.beginSignIn('https://b.test');
    store.setPendingSecret('shl_dev_new');
    store.commit('b@b.test');

    expect(secure.values).toEqual(new Map([[SECRET_KEY, bound('https://b.test', 'shl_dev_new')]]));
    expect(kv.getItem(SERVER_KEY)).toBe('https://b.test');
    expect(kv.getItem(EMAIL_KEY)).toBe('b@b.test');
    expect([kv.getItem(SECRET_KEY), kv.getItem('secret')]).toEqual([null, null]);
  });

  test('clearSecret_WhenSignedIn_ShouldKeepTheServerAndEmail', () => {
    const stores = signedInStores();
    const store = createSessionStore({ ...stores, defaultOrigin: DEFAULT_ORIGIN });

    store.clearSecret();

    expect(store.get()).toMatchObject({
      origin: 'https://a.test',
      email: 'a@a.test',
      secret: null,
      signedIn: false,
    });
    expect(stores.kv.getItem(SERVER_KEY)).toBe('https://a.test');
    expect(stores.kv.getItem(EMAIL_KEY)).toBe('a@a.test');
    expect(stores.secure.values.has(SECRET_KEY)).toBe(false);
    // A fresh launch reads the same: signed out, server and email kept.
    expect(createSessionStore(stores).get()).toMatchObject({
      origin: 'https://a.test',
      email: 'a@a.test',
      signedIn: false,
    });
  });

  test('clearSecret_WhenTheSecureStoreRejects_ShouldStillSignOut', async () => {
    const secure = secureStore({ [SECRET_KEY]: bound('https://a.test', 'shl_dev_old') });
    secure.deleteItemAsync = () => Promise.reject(new Error('keystore locked'));
    const store = createSessionStore({ secure, kv: kvWith({ [SERVER_KEY]: 'https://a.test' }) });

    store.clearSecret();
    await Promise.resolve();

    expect(store.get()).toMatchObject({ secret: null, signedIn: false });
  });

  test('clearSecret_WhenTheSecureStoreThrows_ShouldStillSignOut', () => {
    const secure = secureStore({ [SECRET_KEY]: bound('https://a.test', 'shl_dev_old') });
    secure.deleteItemAsync = () => {
      throw new Error('no native module');
    };
    const store = createSessionStore({ secure, kv: kvWith({ [SERVER_KEY]: 'https://a.test' }) });

    expect(() => store.clearSecret()).not.toThrow();
    expect(store.get()).toMatchObject({ secret: null, signedIn: false });
  });

  test('beginSignIn_WhenSignedOut_ShouldOverlayTheNewServerWithoutSigningIn', () => {
    const store = createSessionStore({
      secure: secureStore(),
      kv: kvWith(),
      defaultOrigin: DEFAULT_ORIGIN,
    });

    store.beginSignIn('https://b.test');
    store.setPendingSecret('shl_dev_new');

    expect(store.get()).toMatchObject({
      origin: DEFAULT_ORIGIN,
      secret: null,
      signedIn: false,
      pending: { origin: 'https://b.test', secret: 'shl_dev_new' },
    });
  });

  test('commit_WhenAnOverlayIsSet_ShouldSignInToTheOverlaidServer', () => {
    const store = createSessionStore({
      secure: secureStore(),
      kv: kvWith(),
      defaultOrigin: DEFAULT_ORIGIN,
    });

    store.beginSignIn('https://b.test');
    store.setPendingSecret('shl_dev_new');
    store.commit('b@b.test');

    expect(store.get()).toEqual({
      origin: 'https://b.test',
      email: 'b@b.test',
      secret: 'shl_dev_new',
      signedIn: true,
      pending: null,
    });
  });

  test('abandon_WhenAnOverlayIsSet_ShouldLeaveTheStoredSessionAsItWas', () => {
    const stores = signedInStores();
    stores.secure.values.delete(SECRET_KEY);
    const store = createSessionStore({ ...stores, defaultOrigin: DEFAULT_ORIGIN });

    store.beginSignIn('https://b.test');
    store.setPendingSecret('shl_dev_new');
    store.abandon();

    expect(store.get()).toEqual({
      origin: 'https://a.test',
      email: 'a@a.test',
      secret: null,
      signedIn: false,
      pending: null,
    });
    expect(stores.secure.values.size).toBe(0);
    expect(stores.kv.getItem(SERVER_KEY)).toBe('https://a.test');
  });

  test('commit_WhenNoSecretIsPending_ShouldThrow', () => {
    const store = createSessionStore({ secure: secureStore(), kv: kvWith() });

    store.beginSignIn('https://b.test');

    expect(() => store.commit('b@b.test')).toThrow();
  });

  // An iOS reinstall: the Keychain survives the uninstall, the key-value store doesn't.
  test('createSessionStore_WhenSecretHasNoStoredServer_ShouldBeSignedOut', () => {
    const secure = secureStore({ [SECRET_KEY]: 'shl_dev_selfhosted' });
    const store = createSessionStore({ secure, kv: kvWith(), defaultOrigin: DEFAULT_ORIGIN });

    expect(store.get()).toEqual({
      origin: DEFAULT_ORIGIN,
      email: '',
      secret: null,
      signedIn: false,
      pending: null,
    });
    expect(secure.values.has(SECRET_KEY)).toBe(false);
  });

  test('createSessionStore_WhenABoundSecretHasNoStoredServer_ShouldSignInToTheBoundOriginOnly', () => {
    const secure = secureStore({ [SECRET_KEY]: bound('https://selfhosted.test', 'shl_dev_s') });
    const store = createSessionStore({ secure, kv: kvWith(), defaultOrigin: DEFAULT_ORIGIN });

    expect(store.get()).toMatchObject({
      origin: 'https://selfhosted.test',
      secret: 'shl_dev_s',
      signedIn: true,
    });
  });

  test.each([
    ['not JSON', '{origin'],
    ['not an object', '"shl_dev_s"'],
    ['no origin', JSON.stringify({ secret: 'shl_dev_s' })],
    ['a non-string secret', JSON.stringify({ origin: 'https://a.test', secret: 7 })],
  ])('createSessionStore_WhenTheSecureValueIsMalformed_ShouldBeSignedOut (%s)', (_, raw) => {
    const store = createSessionStore({
      secure: secureStore({ [SECRET_KEY]: raw }),
      kv: kvWith({ [SERVER_KEY]: 'https://a.test' }),
    });

    expect(store.get()).toMatchObject({ origin: 'https://a.test', secret: null, signedIn: false });
  });

  // A crash mid-commit under the old order, or a later sign-in whose secure write never landed.
  test('createSessionStore_WhenKvServerDiffersFromBoundOrigin_ShouldUseTheBoundOrigin', () => {
    const store = createSessionStore({
      secure: secureStore({ [SECRET_KEY]: bound('https://a.test', 'shl_dev_a') }),
      kv: kvWith({ [SERVER_KEY]: 'https://b.test', [EMAIL_KEY]: 'b@b.test' }),
      defaultOrigin: DEFAULT_ORIGIN,
    });

    expect(store.get()).toMatchObject({
      origin: 'https://a.test',
      secret: 'shl_dev_a',
      signedIn: true,
    });
  });

  test('commit_WhenInterruptedBeforeTheSecureWrite_ShouldStaySignedOut', () => {
    const secure = secureStore();
    secure.setItem = () => {
      throw new Error('killed');
    };
    const kv = kvWith();
    const store = createSessionStore({ secure, kv, defaultOrigin: DEFAULT_ORIGIN });

    store.beginSignIn('https://b.test');
    store.setPendingSecret('shl_dev_new');

    expect(() => store.commit('b@b.test')).toThrow('killed');
    expect(store.get()).toMatchObject({ secret: null, signedIn: false });
    // The relaunch: the server is kept for the prefill, but nothing is signed in.
    expect(createSessionStore({ secure, kv, defaultOrigin: DEFAULT_ORIGIN }).get()).toMatchObject({
      origin: 'https://b.test',
      secret: null,
      signedIn: false,
    });
  });

  test('authHeaders_WhenTheStoredServersDisagree_ShouldSendTheBearerOnlyToTheBoundOrigin', () => {
    const fromStorage = (secureValue: string) =>
      createMobilePlatform({
        session: createSessionStore({
          secure: secureStore({ [SECRET_KEY]: secureValue }),
          kv: kvWith({ [SERVER_KEY]: 'https://b.test' }),
          defaultOrigin: DEFAULT_ORIGIN,
        }),
        kv: memoryStore(),
        connectivity: createConnectivity(),
        lostSession: () => undefined,
      });

    const boundToA = fromStorage(bound('https://a.test', 'shl_dev_a'));
    const legacy = fromStorage('shl_dev_a');

    expect([boundToA.apiOrigin(), boundToA.authHeaders()]).toEqual([
      'https://a.test',
      { Authorization: 'Bearer shl_dev_a' },
    ]);
    expect([legacy.apiOrigin(), legacy.authHeaders()]).toEqual(['https://b.test', {}]);
  });

  test('subscribe_WhenTheStateChanges_ShouldNotifyWithANewSnapshot', () => {
    const store = createSessionStore({ ...signedInStores() });
    const before = store.get();
    let notified = 0;
    const stop = store.subscribe(() => notified++);

    store.clearSecret();
    stop();
    store.beginSignIn('https://b.test');

    expect(notified).toBe(1);
    expect(store.get()).not.toBe(before);
  });
});
