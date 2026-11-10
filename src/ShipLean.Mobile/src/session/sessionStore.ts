// Who the app is signed in as, and where. Pure: storage is injected, so vitest covers it without a phone.
// - The secret lives only in the secure store (Keychain or Keystore), never in the key-value store or anywhere else. It
//   is stored bound to its server, as JSON `{ origin, secret }`, so it is only ever sent to that server. A secure value
//   that doesn't parse to that shape (a bare legacy secret included) counts as signed out.
// - The server and email also live in the key-value store and survive sign-out, but only to prefill Sign in. They never
//   decide where a stored secret goes: on iOS the Keychain outlives an uninstall and the key-value store doesn't.
// - A sign-in in progress overlays its own origin and secret (`pending`), so the exchange and the first `me` reach the
//   new server while the app still counts as signed out. It then commits or is abandoned.
import type { KeyValueStore } from '@shiplean/core/platform';

// The subset of expo-secure-store the store uses: sync reads and writes, and its only delete, which is async.
export interface SecureStore {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  deleteItemAsync(key: string): Promise<void>;
}

export interface PendingSignIn {
  origin: string;
  secret: string | null;
}

export interface SessionState {
  // Signed in: the server bound to the secret. Signed out: the last server, for the prefill, or the default before
  // anything is stored; null only when there is neither.
  origin: string | null;
  // The last signed-in email; '' before the first sign-in.
  email: string;
  secret: string | null;
  signedIn: boolean;
  pending: PendingSignIn | null;
}

export interface SessionStore {
  get(): SessionState;
  subscribe(fn: () => void): () => void;
  beginSignIn(origin: string): void;
  setPendingSecret(secret: string): void;
  commit(email: string): void;
  abandon(): void;
  clearSecret(): void;
}

export interface SessionStoreOptions {
  secure: SecureStore;
  kv: KeyValueStore;
  defaultOrigin?: string;
}

// Secure-store keys allow only letters, digits, '.', '-' and '_'.
export const SECRET_KEY = 'shiplean.deviceSecret';
export const SERVER_KEY = 'shiplean.server';
export const EMAIL_KEY = 'shiplean.email';

export const DEFAULT_ORIGIN = 'https://prod.shiplean.app';

export function createSessionStore({
  secure,
  kv,
  defaultOrigin,
}: SessionStoreOptions): SessionStore {
  const listeners = new Set<() => void>();

  let committed = readCommitted(secure);
  // The prefill while signed out; never paired with a secret.
  let prefillOrigin = committed?.origin ?? kv.getItem(SERVER_KEY) ?? defaultOrigin ?? null;
  let email = kv.getItem(EMAIL_KEY) ?? '';
  let pending: PendingSignIn | null = null;
  let state = snapshot();

  function snapshot(): SessionState {
    return {
      origin: committed?.origin ?? prefillOrigin,
      email,
      secret: committed?.secret ?? null,
      signedIn: committed !== null,
      pending,
    };
  }

  function changed() {
    state = snapshot();
    for (const listener of listeners) listener();
  }

  return {
    get: () => state,
    subscribe(fn) {
      listeners.add(fn);
      return () => {
        listeners.delete(fn);
      };
    },
    beginSignIn(nextOrigin) {
      pending = { origin: nextOrigin, secret: null };
      changed();
    },
    setPendingSecret(nextSecret) {
      if (pending === null) throw new Error('setPendingSecret needs beginSignIn first.');
      pending = { ...pending, secret: nextSecret };
      changed();
    },
    commit(nextEmail) {
      if (pending?.secret == null) throw new Error('commit needs a pending secret.');
      const next: CommittedSession = { origin: pending.origin, secret: pending.secret };
      kv.setItem(SERVER_KEY, next.origin);
      kv.setItem(EMAIL_KEY, nextEmail);
      // The commit point: until this write lands, a relaunch is signed out (or still on the old, self-bound session).
      secure.setItem(SECRET_KEY, JSON.stringify(next));
      committed = next;
      prefillOrigin = next.origin;
      email = nextEmail;
      pending = null;
      changed();
    },
    abandon() {
      pending = null;
      changed();
    },
    clearSecret() {
      if (committed !== null) prefillOrigin = committed.origin;
      committed = null;
      changed();
      // Signed out here whatever the secure store says: a failed delete only leaves a dead secret behind, which the
      // server refuses next launch and the app clears again.
      deleteSecret(secure);
    },
  };
}

interface CommittedSession {
  origin: string;
  secret: string;
}

// The secret and the server it belongs to, or null for anything else, which is also deleted (best effort).
function readCommitted(secure: SecureStore): CommittedSession | null {
  const raw = secure.getItem(SECRET_KEY);
  if (raw === null) return null;
  const parsed = parseCommitted(raw);
  if (parsed === null) deleteSecret(secure);
  return parsed;
}

function parseCommitted(raw: string): CommittedSession | null {
  let value: unknown;
  try {
    value = JSON.parse(raw);
  } catch {
    return null;
  }
  if (typeof value !== 'object' || value === null) return null;
  const { origin, secret } = value as Record<string, unknown>;
  if (typeof origin !== 'string' || typeof secret !== 'string') return null;
  return { origin, secret };
}

function deleteSecret(secure: SecureStore): void {
  try {
    secure.deleteItemAsync(SECRET_KEY).catch(() => undefined);
  } catch {
    // a synchronous throw from the native module: same as a rejection
  }
}
