// The browser sign-in (spec §4.3 and §4.6). Pure: the browser, the network calls, the device name, the cache seeding
// and the randomness are injected.
//
// The secret never reaches the session's committed side until `me` has answered with it: the exchange goes to the new
// server under a pending overlay, and every failure abandons that overlay.
import { APP_REDIRECT, confirmUrl, parseAppRedirect } from '@shiplean/core/handoff';
import type { DeviceSignInDto, ExchangeSignInCodeRequest } from '@shiplean/core/api/deviceSessions';
import type { MeDto } from '@shiplean/core/api/me';
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
  exchange(request: ExchangeSignInCodeRequest): Promise<DeviceSignInDto>;
  // GET /api/me with whatever the session sends now: the pending secret.
  getMe(): Promise<MeDto>;
  seedMe(me: MeDto): void;
  deviceName(): string;
  randomBytes: RandomBytes;
}

export type SignInOutcome =
  | { kind: 'signedIn' }
  // The browser was closed or the person tapped Cancel: back on Sign in, no error.
  | { kind: 'cancelled' }
  | { kind: 'failed'; message: string }
  // A sign-in was already running, or a redirect arrived that no sign-in is waiting for: nothing changed.
  | { kind: 'ignored' };

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
  // A redirect that came back to the app. Counts only for the sign-in that is waiting, with its own state.
  handleRedirect(url: string | undefined): Promise<SignInOutcome>;
}

// Both sides normalised as the Server field's address is, so only a different scheme, host or port differs.
function sameServer(iss: string, origin: string): boolean {
  const a = normaliseServerAddress(iss);
  const b = normaliseServerAddress(origin);
  return a.ok && b.ok && a.origin === b.origin;
}

export function createSignIn(deps: SignInDeps): SignIn {
  let attempt: Attempt | null = null;
  let running = false;

  async function handleRedirect(url: string | undefined): Promise<SignInOutcome> {
    const current = attempt;
    if (current === null) return { kind: 'ignored' };
    // One redirect per sign-in: a replay finds nothing waiting.
    attempt = null;

    const redirect = url === undefined ? null : parseAppRedirect(url);
    if (redirect === null || redirect.state !== current.state) {
      return fail(signInMessages.mismatch);
    }
    // The issuer check (RFC 9207): the page that answered must be the server the person typed. Otherwise a typed
    // server could bounce the browser to another server's confirm page and replay the code and verifier the app then
    // sends it. Error redirects are checked too.
    if (!sameServer(redirect.iss, current.origin)) return fail(signInMessages.mismatch);
    if ('error' in redirect) {
      if (redirect.error === 'cancelled') {
        deps.session.abandon();
        return { kind: 'cancelled' };
      }
      return fail(signInMessages.unsupported);
    }
    return exchangeCode(current, redirect.code);
  }

  function fail(message: string): SignInOutcome {
    deps.session.abandon();
    return { kind: 'failed', message };
  }

  async function exchangeCode(current: Attempt, code: string): Promise<SignInOutcome> {
    try {
      deps.session.beginSignIn(current.origin);
      const { secret } = await deps.exchange({ code, verifier: current.verifier });
      deps.session.setPendingSecret(secret);
      // The pending secret proves itself here, before anything is committed.
      const me = await deps.getMe();
      deps.seedMe(me);
      deps.session.commit(me.email);
      return { kind: 'signedIn' };
    } catch (error) {
      const status = (error as { status?: unknown } | null)?.status;
      return fail(
        typeof status === 'number'
          ? signInMessages.failed(status)
          : signInMessages.unreachable(hostOfOrigin(current.origin)),
      );
    }
  }

  return {
    handleRedirect,
    async signIn(origin) {
      if (running) return { kind: 'ignored' };
      running = true;
      try {
        const pkce = createPkce(deps.randomBytes);
        attempt = { origin, verifier: pkce.verifier, state: pkce.state };
        const url = confirmUrl(origin, {
          challenge: pkce.challenge,
          state: pkce.state,
          device: deps.deviceName(),
        });
        let result: AuthSessionResult;
        try {
          result = await deps.openAuthSession(url, APP_REDIRECT);
        } catch {
          attempt = null;
          return fail(signInMessages.mismatch);
        }
        if (result.type !== 'success') {
          attempt = null;
          deps.session.abandon();
          return { kind: 'cancelled' };
        }
        return await handleRedirect(result.url);
      } finally {
        attempt = null;
        running = false;
      }
    },
  };
}
