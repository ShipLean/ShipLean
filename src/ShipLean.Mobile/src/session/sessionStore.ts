// Who the app is signed in as, and where. Pure: storage is injected, so vitest covers it without a phone.
// - The secret lives only in the secure store (Keychain or Keystore), never in the key-value store or anywhere else.
// - The server and email live in the key-value store and survive sign-out, so Sign in comes back prefilled.
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
  // The committed server, or the default before anything is stored; null only when there is neither.
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

  let origin = kv.getItem(SERVER_KEY) ?? defaultOrigin ?? null;
  let email = kv.getItem(EMAIL_KEY) ?? '';
  let secret = secure.getItem(SECRET_KEY);
  let pending: PendingSignIn | null = null;
  let state = snapshot();

  function snapshot(): SessionState {
    return { origin, email, secret, signedIn: secret !== null && origin !== null, pending };
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
      secure.setItem(SECRET_KEY, pending.secret);
      kv.setItem(SERVER_KEY, pending.origin);
      kv.setItem(EMAIL_KEY, nextEmail);
      origin = pending.origin;
      secret = pending.secret;
      email = nextEmail;
      pending = null;
      changed();
    },
    abandon() {
      pending = null;
      changed();
    },
    clearSecret() {
      secret = null;
      changed();
      // Signed out here whatever the secure store says: a failed delete only leaves a dead secret behind, which the
      // server refuses next launch and the app clears again.
      try {
        secure.deleteItemAsync(SECRET_KEY).catch(() => undefined);
      } catch {
        // a synchronous throw from the native module: same as above
      }
    },
  };
}
