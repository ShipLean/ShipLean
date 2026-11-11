// Sign out against the real session store, the real core client and a fake server at fetch.
import { endCurrentDeviceSession } from '@shiplean/core/api/deviceSessions';
import { configurePlatform, memoryStore } from '@shiplean/core/platform';
import { afterEach, beforeEach, expect, test } from 'vitest';
import { createConnectivity, createMobilePlatform } from '../mobilePlatformCore';
import { TEST_ORIGIN, TEST_SECRET } from '../test-support/builders';
import { apiError, fakeApi, restoreFetch, type ApiTable } from '../test-support/fakeApi';
import { createSessionStore, type SecureStore } from './sessionStore';
import { signOut } from './signOut';

const END = 'DELETE /api/auth/device-sessions/current';

function setup(table: ApiTable) {
  const secureValues = new Map<string, string>();
  const secure: SecureStore = {
    getItem: (k) => secureValues.get(k) ?? null,
    setItem: (k, v) => void secureValues.set(k, v),
    deleteItemAsync: async (k) => void secureValues.delete(k),
  };
  const session = createSessionStore({ secure, kv: memoryStore() });
  session.beginSignIn(TEST_ORIGIN);
  session.setPendingSecret(TEST_SECRET);
  session.commit('joel@shiplean.local');
  configurePlatform(
    createMobilePlatform({
      session,
      kv: memoryStore(),
      connectivity: createConnectivity(),
      lostSession: () => session.clearSecret(),
    }),
  );
  const api = fakeApi(table);
  const run = () =>
    signOut({ endSession: endCurrentDeviceSession, clear: () => session.clearSecret() });
  return { api, session, secureValues, run };
}

beforeEach(() => configurePlatform(null as never));
afterEach(() => restoreFetch());

function signedOutAndNothingStored(s: ReturnType<typeof setup>) {
  expect(s.session.get().signedIn).toBe(false);
  expect(s.session.get().secret).toBeNull();
  expect(s.session.get().origin).toBe(TEST_ORIGIN);
  expect(s.session.get().email).toBe('joel@shiplean.local');
  expect([...s.secureValues.values()].join('\n')).not.toContain('shl_dev_');
}

test('clears after the server ends the session', async () => {
  const s = setup({ [END]: { status: 204 } });
  await s.run();
  const calls = s.api.callsTo(END);
  expect(calls).toHaveLength(1);
  expect(calls[0].headers.Authorization ?? calls[0].headers.authorization).toBe(
    `Bearer ${TEST_SECRET}`,
  );
  signedOutAndNothingStored(s);
});

test('clears when offline', async () => {
  const s = setup({
    [END]: () => {
      throw new TypeError('Network request failed');
    },
  });
  await s.run();
  expect(s.api.callsTo(END)).toHaveLength(1);
  signedOutAndNothingStored(s);
});

test('clears when the server answers 404', async () => {
  const s = setup({ [END]: apiError(404, 'not_found') });
  await s.run();
  expect(s.api.callsTo(END)).toHaveLength(1);
  signedOutAndNothingStored(s);
});

test('clears while the server hangs', () => {
  const s = setup({ [END]: () => new Promise<never>(() => undefined) });
  void s.run();
  // No await: the phone is signed out before the DELETE answers, and the DELETE still carried the bearer.
  signedOutAndNothingStored(s);
  const calls = s.api.callsTo(END);
  expect(calls).toHaveLength(1);
  expect(calls[0].headers.Authorization ?? calls[0].headers.authorization).toBe(
    `Bearer ${TEST_SECRET}`,
  );
});

test('asks the server with signIn and a timeout signal, so a late 401 never signs out a new session', async () => {
  const seen: { signIn?: boolean; signal?: AbortSignal }[] = [];
  await signOut({
    endSession: async (options) => void seen.push(options),
    clear: () => undefined,
  });
  expect(seen).toHaveLength(1);
  expect(seen[0].signIn).toBe(true);
  expect(seen[0].signal).toBeInstanceOf(AbortSignal);
});
