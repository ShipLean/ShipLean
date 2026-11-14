// The one way the core reaches its host (the web SPA or the phone app): storage, connectivity, where the API is, how a
// request proves who it is, and what a lost session does. Until a host configures it, the core runs on in-memory
// storage, online, against its own origin, and a lost session does nothing, so the core's own tests need no host.

// May throw: a browser can refuse storage (private mode, quota), and the caller decides what that means.
export interface KeyValueStore {
  getItem(k: string): string | null;
  setItem(k: string, v: string): void;
  removeItem(k: string): void;
}

export type UnauthorizedReason = 'unauthorized' | 'session_ended' | 'password_change_required';

export interface Platform {
  // Survives a restart. Web: localStorage; mobile: a sync key-value store.
  persistentStore(): KeyValueStore;
  // Lasts as long as this tab or app run. Web: sessionStorage; mobile: in memory.
  sessionStore(): KeyValueStore;
  isOffline(): boolean;
  // Returns the function that stops listening.
  onOnline(listener: () => void): () => void;
  // A request found the session gone (401) or the one-time password still in place (403).
  onUnauthorized(reason: UnauthorizedReason): void;
  // Web '', mobile 'https://host[:port]' with no trailing slash.
  apiOrigin(): string;
  // Headers the host adds to every request. Web { 'X-ShipLean-Viewport': 'phone' } (the cookie is its proof), mobile
  // { Authorization: 'Bearer shl_dev_…' }.
  authHeaders(): Record<string, string>;
  // Web 'same-origin', mobile 'omit' (no cookie ever rides along).
  credentials(): 'same-origin' | 'omit';
}

export function memoryStore(): KeyValueStore {
  const values = new Map<string, string>();
  return {
    getItem: (k) => values.get(k) ?? null,
    setItem: (k, v) => {
      values.set(k, v);
    },
    removeItem: (k) => {
      values.delete(k);
    },
  };
}

function defaultPlatform(): Platform {
  const persistent = memoryStore();
  const session = memoryStore();
  return {
    persistentStore: () => persistent,
    sessionStore: () => session,
    isOffline: () => false,
    onOnline: () => () => undefined,
    onUnauthorized: () => undefined,
    apiOrigin: () => '',
    authHeaders: () => ({}),
    credentials: () => 'same-origin',
  };
}

let current: Platform = defaultPlatform();

// Once, before anything renders or fetches.
export function configurePlatform(p: Platform): void {
  current = p;
}

export function platform(): Platform {
  return current;
}
