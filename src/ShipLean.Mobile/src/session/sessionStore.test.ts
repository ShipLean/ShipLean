import { memoryStore, type KeyValueStore } from '@shiplean/core/platform';
import { describe, expect, test } from 'vitest';
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

function signedInStores() {
  return {
    secure: secureStore({ [SECRET_KEY]: 'shl_dev_old' }),
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

    expect(secure.values).toEqual(new Map([[SECRET_KEY, 'shl_dev_new']]));
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
    const secure = secureStore({ [SECRET_KEY]: 'shl_dev_old' });
    secure.deleteItemAsync = () => Promise.reject(new Error('keystore locked'));
    const store = createSessionStore({ secure, kv: kvWith({ [SERVER_KEY]: 'https://a.test' }) });

    store.clearSecret();
    await Promise.resolve();

    expect(store.get()).toMatchObject({ secret: null, signedIn: false });
  });

  test('clearSecret_WhenTheSecureStoreThrows_ShouldStillSignOut', () => {
    const secure = secureStore({ [SECRET_KEY]: 'shl_dev_old' });
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
