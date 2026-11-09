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
const OLD_SECRET = 'shl_dev_OldSession0000000000000000000000000000000';

function setup({
  table = {},
  earlierSession = false,
  browser,
}: {
  table?: ApiTable;
  earlierSession?: boolean;
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
  configurePlatform(
    createMobilePlatform({
      session,
      kv,
      connectivity: createConnectivity(),
      lostSession: () => session.clearSecret(),
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
  const realCommit = session.commit;
  session.commit = (email) => {
    committed.push(email);
    realCommit(email);
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
  });
  return { flow, session, api, opened, seeded, committed, secureValues };
}

const stateOf = (url: string) => new URL(url).searchParams.get('state') ?? '';

// A browser that comes back with a code for the state it was given.
const approves = (url: string): AuthSessionResult => ({
  type: 'success',
  url: appRedirect({ code: 'the-code', state: stateOf(url) }),
});

beforeEach(() => configurePlatform(null as never));
afterEach(() => {
  restoreFetch();
});

const nothingStored = (s: { secureValues: Map<string, string> }) =>
  expect([...s.secureValues.values()].join('\n')).not.toContain('shl_dev_');

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

  test('signIn_WhenTheBrowserIsClosed_ShouldCancelWithNoErrorAndNoSecret', async () => {
    const s = setup({ browser: () => ({ type: 'dismiss' }) });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({ kind: 'cancelled' });

    expect(s.api.calls).toEqual([]);
    expect(s.session.get()).toMatchObject({ signedIn: false, pending: null });
    nothingStored(s);
  });

  test('signIn_WhenThePageSaysCancelled_ShouldCancelAbandonAndStoreNoSecret', async () => {
    const s = setup({
      browser: (url) => ({
        type: 'success',
        url: appRedirect({ error: 'cancelled', state: stateOf(url) }),
      }),
    });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({ kind: 'cancelled' });

    expect(s.api.calls).toEqual([]);
    expect(s.session.get()).toMatchObject({ signedIn: false, pending: null });
    nothingStored(s);
  });

  test('signIn_WhenThePageSaysUnsupported_ShouldFailAbandonAndStoreNoSecret', async () => {
    const s = setup({
      browser: (url) => ({
        type: 'success',
        url: appRedirect({ error: 'unsupported', state: stateOf(url) }),
      }),
    });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({
      kind: 'failed',
      message: "This server doesn't support the app yet.",
    });

    expect(s.api.calls).toEqual([]);
    expect(s.session.get()).toMatchObject({ signedIn: false, pending: null });
    nothingStored(s);
  });

  test('signIn_WhenTheStateDiffers_ShouldRefuseWithoutExchanging', async () => {
    const s = setup({
      browser: () => ({
        type: 'success',
        url: appRedirect({ code: 'the-code', state: 'attacker' }),
      }),
    });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({
      kind: 'failed',
      message: "Sign-in didn't finish. Try again.",
    });

    expect(s.api.callsTo('POST /api/auth/device-sessions/exchange')).toEqual([]);
    expect(s.session.get()).toMatchObject({ signedIn: false, pending: null });
    nothingStored(s);
  });

  test('signIn_WhenAnErrorRedirectCarriesTheWrongState_ShouldBeAMismatchNotACancel', async () => {
    const s = setup({
      browser: () => ({
        type: 'success',
        url: appRedirect({ error: 'cancelled', state: 'attacker' }),
      }),
    });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({
      kind: 'failed',
      message: signInMessages.mismatch,
    });
  });

  test.each([
    ['another scheme', (state: string) => `evil://signed-in?code=the-code&state=${state}`],
    ['another path', (state: string) => `${APP_REDIRECT}/steal?code=the-code&state=${state}`],
    ['a web page', (state: string) => `https://evil.test/signed-in?code=the-code&state=${state}`],
    ['no state', () => `${APP_REDIRECT}?code=the-code`],
  ])('signIn_WhenTheRedirectHas%s_ShouldRefuseWithoutExchanging', async (_name, build) => {
    const s = setup({ browser: (url) => ({ type: 'success', url: build(stateOf(url)) }) });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({
      kind: 'failed',
      message: signInMessages.mismatch,
    });

    expect(s.api.callsTo('POST /api/auth/device-sessions/exchange')).toEqual([]);
    nothingStored(s);
  });

  test('signIn_WhenSuccessCarriesNoUrl_ShouldCountAsAMismatch', async () => {
    const s = setup({ browser: () => ({ type: 'success' }) });

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({
      kind: 'failed',
      message: signInMessages.mismatch,
    });
    expect(s.api.calls).toEqual([]);
  });

  test('handleRedirect_WhenNoSignInIsInProgress_ShouldChangeNothing', async () => {
    const s = setup({ browser: approves });

    const outcome = await s.flow.handleRedirect(appRedirect({ code: 'x', state: 'old-state' }));

    expect(outcome).toEqual({ kind: 'ignored' });
    expect(s.api.calls).toEqual([]);
    expect(s.session.get()).toMatchObject({ signedIn: false, pending: null });
  });

  test('handleRedirect_WhenReplayedAfterTheSignInFinished_ShouldChangeNothing', async () => {
    let redirect = '';
    const s = setup({
      browser: (url) => {
        redirect = appRedirect({ code: 'the-code', state: stateOf(url) });
        return { type: 'success', url: redirect };
      },
    });
    await s.flow.signIn(NEW_ORIGIN);
    const callsAfter = s.api.calls.length;

    expect(await s.flow.handleRedirect(redirect)).toEqual({ kind: 'ignored' });

    expect(s.api.calls).toHaveLength(callsAfter);
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

    expect(await s.flow.signIn(NEW_ORIGIN)).toEqual({ kind: 'ignored' });

    release({ type: 'cancel' });
    expect(await first).toEqual({ kind: 'cancelled' });
    expect(s.opened).toHaveLength(1);
  });
});
