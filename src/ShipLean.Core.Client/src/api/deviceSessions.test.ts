import { afterEach, expect, test, vi } from 'vitest';
import createCode from '../../../ShipLean.TestSupport/DtoFixtures/deviceSessions/create-sign-in-code-request.json';
import current from '../../../ShipLean.TestSupport/DtoFixtures/deviceSessions/device-session-current.json';
import other from '../../../ShipLean.TestSupport/DtoFixtures/deviceSessions/device-session-other.json';
import signIn from '../../../ShipLean.TestSupport/DtoFixtures/deviceSessions/device-sign-in.json';
import exchange from '../../../ShipLean.TestSupport/DtoFixtures/deviceSessions/exchange-sign-in-code-request.json';
import code from '../../../ShipLean.TestSupport/DtoFixtures/deviceSessions/sign-in-code.json';
import pinned from '../../../ShipLean.TestSupport/PinnedShapes/me.json';
import { challengeFor, isVerifier } from '../handoff';
import { configurePlatform, platform, type Platform, type UnauthorizedReason } from '../platform';
import {
  createSignInCode,
  deviceSessionKeys,
  endCurrentDeviceSession,
  exchangeSignInCode,
  listDeviceSessions,
  revokeDeviceSession,
  type CreateSignInCodeRequest,
  type DeviceSessionDto,
  type DeviceSignInDto,
  type ExchangeSignInCodeRequest,
  type SignInCodeDto,
} from './deviceSessions';

// Typecheck fails if the fixture lacks a field, has one of the wrong type, or has one the type doesn't declare.
type DeepWiden<T> = T extends string
  ? string
  : T extends number
    ? number
    : T extends boolean
      ? boolean
      : T extends readonly (infer U)[]
        ? DeepWiden<U>[]
        : T extends object
          ? { [K in keyof T]: DeepWiden<T[K]> }
          : T;
type NoExtraKeys<U, T> = U extends readonly (infer UE)[]
  ? T extends readonly (infer TE)[]
    ? NoExtraKeys<UE, TE>[]
    : U
  : U extends object
    ? T extends object
      ? { [K in keyof U]: K extends keyof T ? NoExtraKeys<U[K], T[K]> : never }
      : U
    : U;

function typed<T>() {
  return <U extends DeepWiden<T>>(value: U & NoExtraKeys<U, DeepWiden<T>>): DeepWiden<T> => value;
}

// What a fixture looks like after crossing the wire: the fetch wrapper answers with the parsed body.
async function roundTrip<T>(fixture: unknown, call: () => Promise<T>): Promise<T> {
  stubFetch(new Response(JSON.stringify(fixture), { status: 200 }));
  return call();
}

function stubFetch(response: Response) {
  const fetchStub = vi.fn().mockResolvedValue(response);
  vi.stubGlobal('fetch', fetchStub);
  return fetchStub;
}

function sent(fetchStub: ReturnType<typeof stubFetch>) {
  const [url, init] = fetchStub.mock.calls[0] as [string, RequestInit];
  return {
    url,
    method: init.method,
    body: init.body === undefined ? undefined : JSON.parse(String(init.body)),
  };
}

const original = platform();
afterEach(() => {
  vi.unstubAllGlobals();
  configurePlatform(original);
});

test('deviceSessionFixtures_ShouldTypeEveryShape', () => {
  for (const session of [current, other, signIn.session]) typed<DeviceSessionDto>()(session);
  typed<DeviceSignInDto>()(signIn);
  typed<CreateSignInCodeRequest>()(createCode);
  typed<ExchangeSignInCodeRequest>()(exchange);
  typed<SignInCodeDto>()(code);

  expect(current.current).toBe(true);
  expect(other.current).toBe(false);
  expect(signIn.me).toEqual(pinned.me);
  expect(signIn.secret).toMatch(/^shl_dev_[A-Za-z0-9]{43}$/);
  expect(signIn.session.tokenPrefix).toBe(signIn.secret.slice(0, 16));
  for (const session of [current, other, signIn.session])
    expect(session.tokenPrefix).toMatch(/^shl_dev_[A-Za-z0-9]{8}$/);
  expect(isVerifier(exchange.verifier)).toBe(true);
  expect(challengeFor(exchange.verifier)).toBe(createCode.challenge);
});

test('listDeviceSessions_ShouldGetTheListAndRoundTripEachSession', async () => {
  const fetchStub = stubFetch(new Response(JSON.stringify([current, other]), { status: 200 }));

  const sessions = await listDeviceSessions();

  expect(sessions).toEqual([current, other]);
  expect(sent(fetchStub)).toEqual({
    url: '/api/auth/device-sessions',
    method: 'GET',
    body: undefined,
  });
  expect(deviceSessionKeys.list).toEqual(['auth', 'device-sessions']);
});

test('createSignInCode_ShouldPostTheRequestAndRoundTripTheCode', async () => {
  expect(await roundTrip(code, () => createSignInCode(createCode))).toEqual(code);

  const fetchStub = stubFetch(new Response(JSON.stringify(code), { status: 200 }));
  await createSignInCode(createCode);
  expect(sent(fetchStub)).toEqual({
    url: '/api/auth/device-sessions/codes',
    method: 'POST',
    body: createCode,
  });
});

test('exchangeSignInCode_ShouldPostTheRequestAndRoundTripTheSignIn', async () => {
  expect(await roundTrip(signIn, () => exchangeSignInCode(exchange))).toEqual(signIn);

  const fetchStub = stubFetch(new Response(JSON.stringify(signIn), { status: 201 }));
  await exchangeSignInCode(exchange);
  expect(sent(fetchStub)).toEqual({
    url: '/api/auth/device-sessions/exchange',
    method: 'POST',
    body: exchange,
  });
});

test('exchangeSignInCode_WhenRefused_ShouldNotCallOnUnauthorized', async () => {
  const reasons: UnauthorizedReason[] = [];
  const host: Platform = { ...original, onUnauthorized: (reason) => reasons.push(reason) };
  configurePlatform(host);

  for (const [status, errorCode] of [
    [400, 'validation'],
    [401, 'unauthorized'],
    [401, 'session_ended'],
  ] as const) {
    stubFetch(
      new Response(JSON.stringify({ error: 'Sign-in expired. Try again.', code: errorCode }), {
        status,
      }),
    );
    const error = await exchangeSignInCode(exchange).catch((e: unknown) => e);
    expect(error).toMatchObject({ status, code: errorCode });
  }

  expect(reasons).toEqual([]);
});

test('revokeDeviceSession_ShouldDeleteThatSession', async () => {
  const fetchStub = stubFetch(new Response(null, { status: 204 }));

  expect(await revokeDeviceSession(other.id)).toBeUndefined();

  expect(sent(fetchStub)).toEqual({
    url: `/api/auth/device-sessions/${other.id}`,
    method: 'DELETE',
    body: undefined,
  });
});

test('endCurrentDeviceSession_ShouldDeleteTheCurrentSession', async () => {
  const fetchStub = stubFetch(new Response(null, { status: 204 }));

  expect(await endCurrentDeviceSession()).toBeUndefined();

  expect(sent(fetchStub)).toEqual({
    url: '/api/auth/device-sessions/current',
    method: 'DELETE',
    body: undefined,
  });
});
