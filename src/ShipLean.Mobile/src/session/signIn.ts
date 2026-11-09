// The browser sign-in (spec §4.3 and §4.6). Pure: the browser, the network calls, the device name, the cache seeding
// and the randomness are injected.
//
// The secret never reaches the session's committed side until `me` has answered with it: the exchange goes to the new
// server under a pending overlay, and every failure after beginSignIn abandons that overlay. Cancel, unsupported and
// a mismatch are settled before beginSignIn, so they leave the session as it was.
//
// A redirect counts only as the answer to the signIn call that opened the browser: there is no other way in. A stray
// shiplean://signed-in deep link with no sign-in running is kept off the router by app/+native-intent.ts
// (nativeIntent.test.ts) and never reaches this flow.
import { api, ApiRequestError, type RequestOptions } from '@shiplean/core/api/client';
import type { DeviceSignInDto, ExchangeSignInCodeRequest } from '@shiplean/core/api/deviceSessions';
import type { MeDto } from '@shiplean/core/api/me';
import { APP_REDIRECT, confirmUrl, parseAppRedirect } from '@shiplean/core/handoff';
import { createPkce, type RandomBytes } from './pkce';
import { hostOfOrigin, normaliseServerAddress } from './serverAddress';
import type { SessionStore } from './sessionStore';

export interface AuthSessionResult {
  type: string;
  url?: string;
}

export interface SignInDeps {
  session: Pick<SessionStore, 'beginSignIn' | 'setPendingSecret' | 'commit' | 'abandon'>;
  openAuthSession(url: string, redirect: string): Promise<AuthSessionResult>;
  // Both are sent with { signIn: true }, so a refusal is the sign-in's and never a lost session, and a timeout signal.
  exchange(request: ExchangeSignInCodeRequest, options: RequestOptions): Promise<DeviceSignInDto>;
  // GET /api/me with whatever the session sends now: the pending secret.
  getMe(options: RequestOptions): Promise<MeDto>;
  seedMe(me: MeDto): void;
  deviceName(): string;
  randomBytes: RandomBytes;
  // How long the exchange and `me` may each take before the sign-in fails as unreachable. SIGN_IN_TIMEOUT_MS if left out.
  timeoutMs?: number;
}

export const SIGN_IN_TIMEOUT_MS = 15_000;

// The exchange and `me` through the core client. The core's exchangeSignInCode and getMe take no options, and these
// calls need a signal and, for `me`, signIn: true.
export const coreSignInCalls: Pick<SignInDeps, 'exchange' | 'getMe'> = {
  exchange: (request, options) =>
    api.post<DeviceSignInDto>('/api/auth/device-sessions/exchange', request, options),
  getMe: (options) => api.get<MeDto>('/api/me', options),
};

export type SignInOutcome =
  | { kind: 'signedIn' }
  // The browser was closed or the person tapped Cancel: back on Sign in, no error.
  | { kind: 'cancelled' }
  | { kind: 'failed'; message: string }
  // A sign-in was already running (a second tap): nothing changed.
  | { kind: 'busy' };

export const signInMessages = {
  unsupported: "This server doesn't support the app yet.",
  mismatch: "Sign-in didn't finish. Try again.",
  unreachable: (host: string) => `Couldn't reach ${host}. Check the address and your connection.`,
  failed: (status: number) => `Sign-in failed (${status}). Try again.`,
};

interface Attempt {
  origin: string;
  verifier: string;
  state: string;
}

export interface SignIn {
  signIn(origin: string): Promise<SignInOutcome>;
}

// The exchange or `me` took longer than the timeout.
class SignInTimeout extends Error {}

// Both sides normalised as the Server field's address is, so only a different scheme, host or port differs.
function sameServer(iss: string, origin: string): boolean {
  const a = normaliseServerAddress(iss);
  const b = normaliseServerAddress(origin);
  return a.ok && b.ok && a.origin === b.origin;
}

export function createSignIn(deps: SignInDeps): SignIn {
  const timeoutMs = deps.timeoutMs ?? SIGN_IN_TIMEOUT_MS;
  let running = false;

  const failed = (message: string): SignInOutcome => ({ kind: 'failed', message });

  // The redirect the browser came back with, for the attempt that opened it.
  async function finish(attempt: Attempt, url: string | undefined): Promise<SignInOutcome> {
    const redirect = url === undefined ? null : parseAppRedirect(url);
    if (redirect === null || redirect.state !== attempt.state)
      return failed(signInMessages.mismatch);
    // The issuer check (RFC 9207): the page that answered must be the server the person typed. Otherwise a typed
    // server could bounce the browser to another server's confirm page and replay the code and verifier the app then
    // sends it. Error redirects are checked too.
    if (!sameServer(redirect.iss, attempt.origin)) return failed(signInMessages.mismatch);
    if ('error' in redirect) {
      return redirect.error === 'cancelled'
        ? { kind: 'cancelled' }
        : failed(signInMessages.unsupported);
    }
    return exchangeCode(attempt, redirect.code);
  }

  async function timed<T>(call: (options: RequestOptions) => Promise<T>): Promise<T> {
    const signal = AbortSignal.timeout(timeoutMs);
    try {
      return await call({ signIn: true, signal });
    } catch (error) {
      if (signal.aborted) throw new SignInTimeout();
      throw error;
    }
  }

  async function exchangeCode(attempt: Attempt, code: string): Promise<SignInOutcome> {
    try {
      deps.session.beginSignIn(attempt.origin);
      const { secret } = await timed((options) =>
        deps.exchange({ code, verifier: attempt.verifier }, options),
      );
      deps.session.setPendingSecret(secret);
      // The pending secret proves itself here, before anything is committed.
      const me = await timed((options) => deps.getMe(options));
      deps.seedMe(me);
      deps.session.commit(me.email);
      return { kind: 'signedIn' };
    } catch (error) {
      deps.session.abandon();
      if (error instanceof ApiRequestError) return failed(signInMessages.failed(error.status));
      // fetch rejects with a TypeError when the server can't be reached; a timeout reads the same to the person.
      if (error instanceof TypeError || error instanceof SignInTimeout) {
        return failed(signInMessages.unreachable(hostOfOrigin(attempt.origin)));
      }
      throw error;
    }
  }

  return {
    async signIn(origin) {
      if (running) return { kind: 'busy' };
      running = true;
      try {
        const pkce = createPkce(deps.randomBytes);
        const attempt: Attempt = { origin, verifier: pkce.verifier, state: pkce.state };
        const url = confirmUrl(origin, {
          challenge: pkce.challenge,
          state: pkce.state,
          device: deps.deviceName(),
        });
        let result: AuthSessionResult;
        try {
          result = await deps.openAuthSession(url, APP_REDIRECT);
        } catch {
          return failed(signInMessages.mismatch);
        }
        if (result.type !== 'success') return { kind: 'cancelled' };
        return await finish(attempt, result.url);
      } finally {
        running = false;
      }
    },
  };
}
