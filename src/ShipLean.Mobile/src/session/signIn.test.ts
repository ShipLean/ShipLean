// The sign-in flow against the real session store, the real core client and a fake server at fetch. Only the browser,
// the device name, the randomness and the cache are fakes.
import { exchangeSignInCode } from '@shiplean/core/api/deviceSessions';
import { getMe } from '@shiplean/core/api/me';
import { appRedirect, challengeFor, confirmUrl, APP_REDIRECT } from '@shiplean/core/handoff';
import { configurePlatform, memoryStore } from '@shiplean/core/platform';
import { afterEach, beforeEach, describe, expect, test } from 'vitest';
import { createConnectivity, createMobilePlatform } from '../mobilePlatformCore';
import { TEST_SECRET, aDeviceSignIn, aMe } from '../test-support/builders';
import {
  apiError,
  fakeApi,
  restoreFetch,
  type ApiTable,
  type FakeApi,
} from '../test-support/fakeApi';
import { normaliseServerAddress } from './serverAddress';
import { createSignIn, signInMessages, type AuthSessionResult } from './signIn';
import {
  EMAIL_KEY,
  SECRET_KEY,
  SERVER_KEY,
  createSessionStore,
  type SecureStore,
} from './sessionStore';

const NEW_ORIGIN = 'https://new.test';
const OLD_ORIGIN = 'https://old.test';
const A_ORIGIN = 'https://a.test';
const B_ORIGIN = 'https://b.test';
// The new server's iss, as a query pair.
const ISS = `iss=${encodeURIComponent(NEW_ORIGIN)}`;
const OLD_SECRET = 'shl_dev_OldSession0000000000000000000000000000000';

function setup({
  table = {},
  earlierSession = false,
  timeoutMs,
  browser,
}: {
  table?: ApiTable;
  earlierSession?: boolean;
  timeoutMs?: number;
  // What the browser does, given the URL it was opened with.
  browser: (url: string) => AuthSessionResult | Promise<AuthSessionResult>;
}) {
  const secureValues = new Map<string, string>();
  const secure: SecureStore = {
    getItem: (k) => secureValues.get(k) ?? null,
    setItem: (k, v) => void secureValues.set(k, v),
    deleteItemAsync: async (k) => void secureValues.delete(k),
  };
  const kv = memoryStore();
  if (earlierSession) {
    secureValues.set(SECRET_KEY, JSON.stringify({ origin: OLD_ORIGIN, secret: OLD_SECRET }));
    kv.setItem(SERVER_KEY, OLD_ORIGIN);
    kv.setItem(EMAIL_KEY, 'old@old.test');
  }
  const session = createSessionStore({ secure, kv });
  const lost: unknown[] = [];
  configurePlatform(
    createMobilePlatform({
      session,
      kv,
      connectivity: createConnectivity(),
      lostSession: () => {
        lost.push(true);
        session.clearSecret();
      },
    }),
  );
  const api: FakeApi = fakeApi({
    'POST /api/auth/device-sessions/exchange': aDeviceSignIn(),
    'GET /api/me': aMe(),
    ...table,
  });
  const opened: string[] = [];
  const seeded: unknown[] = [];
  const committed: string[] = [];
  const began: string[] = [];
  const realCommit = session.commit;
  session.commit = (email) => {
    committed.push(email);
    realCommit(email);
  };
  const realBegin = session.beginSignIn;
  session.beginSignIn = (origin) => {
    began.push(origin);
    realBegin(origin);
  };
  let bytes = 0;
  const flow = createSignIn({
    session,
    openAuthSession: async (url) => {
      opened.push(url);
      return browser(url);
    },
    exchange: exchangeSignInCode,
    getMe,
    seedMe: (me) => seeded.push(me),
    deviceName: () => 'Pixel 9',
    randomBytes: (n) => Uint8Array.from({ length: n }, () => bytes++ * 5 + 1),
    timeoutMs,
  });
  return { flow, session, api, opened, seeded, committed, began, lost, secureValues };
}

const stateOf = (url: string) => new URL(url).searchParams.get('state') ?? '';

// A browser that comes back with a code for the state it was given.
// The confirm page's own origin, as it puts it in the redirect's iss.
const issOf = (url: string) => new URL(url).origin;

const approves = (url: string): AuthSessionResult => ({
  type: 'success',
  url: appRedirect({ code: 'the-code', state: stateOf(url), iss: issOf(url) }),
});

beforeEach(() => configurePlatform(null as never));
afterEach(() => {
  restoreFetch();
});

const nothingStored = (s: { secureValues: Map<string, string> }) =>
  expect([...s.secureValues.values()].join('\n')).not.toContain('shl_dev_');

// Settled before the exchange: the session was never touched.
const neverBegan = (s: ReturnType<typeof setup>) => {
  expect(s.began).toEqual([]);
  expect(s.session.get()).toMatchObject({ signedIn: false, pending: null });
  nothingStored(s);
};

// A server that never answers `path`: like a real fetch, it rejects only when the request's signal fires.
function hangs(path: string) {
  const answering = globalThis.fetch;
  globalThis.fetch = ((input: string, init?: RequestInit) => {
    if (!input.endsWith(path)) return answering(input, init);
    return new Promise((_resolve, reject) => {
      init?.signal?.addEventListener('abort', () => reject(init.signal?.reason));
    });
  }) as typeof fetch;
}

// A redirect with no sign-in running, or a replay of an old one, never reaches the flow: the only redirect it reads is
// the one openAuthSession hands back to the signIn call that opened it, and app/+native-intent.ts keeps a stray
// shiplean://signed-in deep link off the router (nativeIntent.test.ts).

describe('createSignIn', () => {
  test('signIn_WhenTheBrowserApproves_ShouldCommitAfterMeAnswers', async () => {
    const s = setup({ browser: approves });

    const outcome = await s.flow.signIn(NEW_ORIGIN);

    expect(outcome).toEqual({ kind: 'signedIn' });
    expect(s.session.get()).toMatchObject({
      signedIn: true,
      origin: NEW_ORIGIN,
      secret: TEST_SECRET,
      email: aMe().email,
      pending: null,
    });
    expect(s.seeded).toEqual([aMe()]);
    expect(s.committed).toEqual([aMe().email]);
    expect(s.api.callsTo('GET /api/me')[0].headers.Authorization).toBe(`Bearer ${TEST_SECRET}`);
  });

  test('signIn_ShouldOpenTheConfirmPageWithAChallengeForTheVerifierItExchanges', async () => {
    const s = setup({ browser: approves });

    await s.flow.signIn(NEW_ORIGIN);

    const url = new URL(s.opened[0]);
    expect(`${url.origin}${url.pathname}`).toBe(`${NEW_ORIGIN}/mobile/sign-in`);
    expect(url.searchParams.get('device')).toBe('Pixel 9');
    const exchange = s.api.callsTo('POST /api/auth/device-sessions/exchange')[0];
    expect(exchange.body).toMatchObject({ code: 'the-code' });
    const { verifier } = exchange.body as { verifier: string };
    // The URL carries the challenge and state the exchange's verifier belongs to.
    expect(s.opened[0]).toBe(
      confirmUrl(NEW_ORIGIN, {
        challenge: challengeFor(verifier),
        state: stateOf(s.opened[0]),
        device: 'Pixel 9',
      }),
    );
  });

  test('signIn_WhenTheBrowserIsClosed_ShouldCancelStoreNoSecretAndNeverBeginSignIn', async () => {
    const s = setup({ browser: () => ({ type: 'dismiss' }) });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({ kind: 'cancelled' });

    expect(s.api.calls).toEqual([]);
    neverBegan(s);
  });

  test('signIn_WhenThePageSaysCancelled_ShouldCancelStoreNoSecretAndNeverBeginSignIn', async () => {
    const s = setup({
      browser: (url) => ({
        type: 'success',
        url: appRedirect({ error: 'cancelled', state: stateOf(url), iss: issOf(url) }),
      }),
    });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({ kind: 'cancelled' });

    expect(s.api.calls).toEqual([]);
    neverBegan(s);
  });

  test('signIn_WhenThePageSaysUnsupported_ShouldFailStoreNoSecretAndNeverBeginSignIn', async () => {
    const s = setup({
      browser: (url) => ({
        type: 'success',
        url: appRedirect({ error: 'unsupported', state: stateOf(url), iss: issOf(url) }),
      }),
    });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({
      kind: 'failed',
      message: "This server doesn't support the app yet.",
    });

    expect(s.api.calls).toEqual([]);
    neverBegan(s);
  });

  test('signIn_WhenTheStateDiffers_ShouldRefuseStoreNoSecretAndNeverBeginSignIn', async () => {
    const s = setup({
      browser: (url) => ({
        type: 'success',
        url: appRedirect({ code: 'the-code', state: 'attacker', iss: issOf(url) }),
      }),
    });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({
      kind: 'failed',
      message: "Sign-in didn't finish. Try again.",
    });

    expect(s.api.callsTo('POST /api/auth/device-sessions/exchange')).toEqual([]);
    neverBegan(s);
  });

  test('signIn_WhenAnErrorRedirectCarriesTheWrongState_ShouldBeAMismatchNotACancel', async () => {
    const s = setup({
      browser: (url) => ({
        type: 'success',
        url: appRedirect({ error: 'cancelled', state: 'attacker', iss: issOf(url) }),
      }),
    });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({
      kind: 'failed',
      message: signInMessages.mismatch,
    });
  });

  test.each([
    ['another scheme', (state: string) => `evil://signed-in?code=the-code&state=${state}&${ISS}`],
    [
      'another path',
      (state: string) => `${APP_REDIRECT}/steal?code=the-code&state=${state}&${ISS}`,
    ],
    [
      'a web page',
      (state: string) => `https://evil.test/signed-in?code=the-code&state=${state}&${ISS}`,
    ],
    ['no state', () => `${APP_REDIRECT}?code=the-code&${ISS}`],
    ['no iss', (state: string) => `${APP_REDIRECT}?code=the-code&state=${state}`],
  ])('signIn_WhenTheRedirectHas%s_ShouldRefuseWithoutExchanging', async (_name, build) => {
    const s = setup({ browser: (url) => ({ type: 'success', url: build(stateOf(url)) }) });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({
      kind: 'failed',
      message: signInMessages.mismatch,
    });

    expect(s.api.callsTo('POST /api/auth/device-sessions/exchange')).toEqual([]);
    nothingStored(s);
  });

  test('signIn_WhenRedirectIssuerIsAnotherServer_ShouldRefuseWithoutExchange', async () => {
    // The mix-up: the person typed server A, A bounced the browser to the real server B, and B's page approved. The
    // state and code are good; only the iss says the code came from B.
    const s = setup({
      browser: (url) => ({
        type: 'success',
        url: appRedirect({ code: 'b-code', state: stateOf(url), iss: B_ORIGIN }),
      }),
    });

    expect(await s.flow.signIn(A_ORIGIN)).toEqual({
      kind: 'failed',
      message: signInMessages.mismatch,
    });

    expect(s.api.calls.filter((c) => c.method === 'POST' && c.path.endsWith('/exchange'))).toEqual(
      [],
    );
    expect(s.session.get()).toMatchObject({ signedIn: false, pending: null, secret: null });
    nothingStored(s);
    expect(s.began).toEqual([]);
  });

  test('signIn_WhenRedirectIssuerIsTheTypedServer_ShouldExchangeThere', async () => {
    const s = setup({
      browser: (url) => ({
        type: 'success',
        // Written differently from the typed origin, but the same server once normalised.
        url: appRedirect({ code: 'the-code', state: stateOf(url), iss: 'https://A.test' }),
      }),
    });

    expect(await s.flow.signIn(A_ORIGIN)).toEqual({ kind: 'signedIn' });

    const exchange = s.api.callsTo('POST /api/auth/device-sessions/exchange');
    expect(exchange.map((c) => c.origin)).toEqual([A_ORIGIN]);
  });

  test('signIn_WhenTheTypedAddressHasTheDefaultPort_ShouldMatchTheIssWithoutOne', async () => {
    const s = setup({
      browser: (url) => ({
        type: 'success',
        url: appRedirect({ code: 'the-code', state: stateOf(url), iss: A_ORIGIN }),
      }),
    });
    // The Server field normalises first, so the flow is handed the origin it makes from the typed address.
    const typed = normaliseServerAddress('https://a.test:443');
    expect(typed.ok && typed.origin).toBe(A_ORIGIN);

    expect(await s.flow.signIn(typed.ok ? typed.origin : '')).toEqual({ kind: 'signedIn' });
  });

  test.each([['cancelled' as const], ['unsupported' as const]])(
    'signIn_WhenAnErrorRedirectsIssuerIsAnotherServer_ShouldBeAMismatch (%s)',
    async (error) => {
      const s = setup({
        browser: (url) => ({
          type: 'success',
          url: appRedirect({ error, state: stateOf(url), iss: B_ORIGIN }),
        }),
      });

      expect(await s.flow.signIn(A_ORIGIN)).toEqual({
        kind: 'failed',
        message: signInMessages.mismatch,
      });
      expect(s.api.calls).toEqual([]);
    },
  );

  test('signIn_WhenSuccessCarriesNoUrl_ShouldCountAsAMismatch', async () => {
    const s = setup({ browser: () => ({ type: 'success' }) });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({
      kind: 'failed',
      message: signInMessages.mismatch,
    });
    expect(s.api.calls).toEqual([]);
  });

  test('signIn_WhenTheExchangeIsRefused_ShouldShowTheStatusAbandonAndStoreNoSecret', async () => {
    const s = setup({
      browser: approves,
      table: { 'POST /api/auth/device-sessions/exchange': apiError(400, 'validation') },
    });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({
      kind: 'failed',
      message: 'Sign-in failed (400). Try again.',
    });

    expect(s.session.get()).toMatchObject({ signedIn: false, pending: null, secret: null });
    expect(s.committed).toEqual([]);
    nothingStored(s);
  });

  test('signIn_WhenTheServerCannotBeReached_ShouldSayTheHostAndAbandon', async () => {
    const s = setup({
      browser: approves,
      table: {
        'POST /api/auth/device-sessions/exchange': () => {
          throw new TypeError('Network request failed');
        },
      },
    });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({
      kind: 'failed',
      message: "Couldn't reach new.test. Check the address and your connection.",
    });

    expect(s.session.get()).toMatchObject({ signedIn: false, pending: null });
    nothingStored(s);
  });

  test.each([
    ['TheExchange', '/api/auth/device-sessions/exchange'],
    ['Me', '/api/me'],
  ])('signIn_WhenTheServerHangsOn%s_ShouldTimeOutAsUnreachableAndAbandon', async (_name, path) => {
    const s = setup({ browser: approves, timeoutMs: 20 });
    hangs(path);

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({
      kind: 'failed',
      message: "Couldn't reach new.test. Check the address and your connection.",
    });

    expect(s.committed).toEqual([]);
    expect(s.session.get()).toMatchObject({ signedIn: false, pending: null, secret: null });
    nothingStored(s);
  });

  test('signIn_WhenTheExchangeThrowsSomethingElse_ShouldAbandonAndRethrow', async () => {
    const s = setup({
      browser: approves,
      table: {
        'POST /api/auth/device-sessions/exchange': () => {
          throw new RangeError('a bug, not the network');
        },
      },
    });

    await expect(s.flow.signIn(NEW_ORIGIN)).rejects.toThrow('a bug, not the network');

    expect(s.began).toEqual([NEW_ORIGIN]);
    expect(s.session.get()).toMatchObject({ signedIn: false, pending: null });
    nothingStored(s);
  });

  test('signIn_WhenMeIsRefusedWithThePendingSecret_ShouldNeverCommitAndAbandon', async () => {
    const s = setup({
      browser: approves,
      table: { 'GET /api/me': apiError(401, 'unauthorized') },
    });

    const outcome = await s.flow.signIn(NEW_ORIGIN);

    expect(outcome).toEqual({ kind: 'failed', message: 'Sign-in failed (401). Try again.' });
    // me was asked for with the pending secret, and nothing was committed or stored after it refused.
    expect(s.api.callsTo('GET /api/me')).toHaveLength(1);
    expect(s.committed).toEqual([]);
    expect(s.seeded).toEqual([]);
    expect(s.session.get()).toMatchObject({ signedIn: false, pending: null, secret: null });
    nothingStored(s);
    // Sent as a sign-in call: its 401 is this sign-in's refusal, not a lost session.
    expect(s.lost).toEqual([]);
  });

  test('signIn_WhileMeIsAsked_ShouldHoldTheSecretOnlyAsPending', async () => {
    let seenWhileMe: unknown;
    let read: () => unknown = () => null;
    const s = setup({
      browser: approves,
      table: {
        'GET /api/me': () => {
          seenWhileMe = read();
          return aMe();
        },
      },
    });
    read = () => s.session.get();

    await s.flow.signIn(NEW_ORIGIN);

    expect(seenWhileMe).toMatchObject({
      signedIn: false,
      secret: null,
      pending: { origin: NEW_ORIGIN, secret: TEST_SECRET },
    });
  });

  test('signIn_WhenAnEarlierSessionExists_ShouldExchangeAtTheNewOriginWithNoBearer', async () => {
    const s = setup({ browser: approves, earlierSession: true });
    s.session.clearSecret();

    await s.flow.signIn(NEW_ORIGIN);

    const exchange = s.api.callsTo('POST /api/auth/device-sessions/exchange')[0];
    expect(exchange.origin).toBe(NEW_ORIGIN);
    expect(exchange.headers.Authorization).toBeUndefined();
    expect(JSON.stringify(s.api.calls)).not.toContain(OLD_SECRET);
  });

  test('signIn_WhenSignedInBeforeToAnotherServer_ShouldNotSendTheOldSecretToTheNewOne', async () => {
    const s = setup({ browser: approves, earlierSession: true });

    await s.flow.signIn(NEW_ORIGIN);

    const exchange = s.api.callsTo('POST /api/auth/device-sessions/exchange')[0];
    expect(exchange.origin).toBe(NEW_ORIGIN);
    expect(exchange.headers.Authorization).toBeUndefined();
  });

  test('signIn_WhenTheExchangeFailsOverAnEarlierSession_ShouldKeepTheEarlierSessionIntact', async () => {
    const s = setup({
      browser: approves,
      earlierSession: true,
      table: { 'POST /api/auth/device-sessions/exchange': apiError(400, 'validation') },
    });

    await s.flow.signIn(NEW_ORIGIN);

    expect(s.session.get()).toMatchObject({
      signedIn: true,
      origin: OLD_ORIGIN,
      secret: OLD_SECRET,
      pending: null,
    });
  });

  test('signIn_WhenAnotherIsRunning_ShouldIgnoreTheSecondCall', async () => {
    let release: (r: AuthSessionResult) => void = () => undefined;
    const s = setup({ browser: () => new Promise((resolve) => (release = resolve)) });
    const first = s.flow.signIn(NEW_ORIGIN);

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({ kind: 'busy' });

    release({ type: 'cancel' });
    expect(await first).toEqual({ kind: 'cancelled' });
    expect(s.opened).toHaveLength(1);
  });
});
