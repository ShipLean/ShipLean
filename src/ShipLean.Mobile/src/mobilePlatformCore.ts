// The pure part of the app's Platform (K2): no React Native here, so vitest covers it.
import { memoryStore, type KeyValueStore, type Platform } from '@shiplean/core/platform';
import type { SessionStore } from './session/sessionStore';

export interface Connectivity {
  isOffline(): boolean;
  onOnline(listener: () => void): () => void;
  // NetInfo's isConnected: false is offline; true or null (not known yet) is online.
  update(isConnected: boolean | null): void;
}

// Online until told otherwise. Listeners hear only the step from offline back to online.
export function createConnectivity(): Connectivity {
  const listeners = new Set<() => void>();
  let offline = false;
  return {
    isOffline: () => offline,
    onOnline(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    update(isConnected) {
      const wasOffline = offline;
      offline = isConnected === false;
      if (wasOffline && !offline) for (const listener of [...listeners]) listener();
    },
  };
}

export interface MobilePlatformOptions {
  session: Pick<SessionStore, 'get'>;
  kv: KeyValueStore;
  connectivity: Connectivity;
  lostSession: () => void;
}

export function createMobilePlatform({
  session,
  kv,
  connectivity,
  lostSession,
}: MobilePlatformOptions): Platform {
  const memory = memoryStore();
  return {
    persistentStore: () => kv,
    sessionStore: () => memory,
    isOffline: () => connectivity.isOffline(),
    onOnline: (listener) => connectivity.onOnline(listener),
    onUnauthorized: () => lostSession(),
    // A sign-in in progress talks to its own server with its own secret (or none yet).
    apiOrigin: () => {
      const state = session.get();
      return state.pending?.origin ?? state.origin ?? '';
    },
    authHeaders: (): Record<string, string> => {
      const state = session.get();
      const secret = state.pending ? state.pending.secret : state.secret;
      return secret === null ? {} : { Authorization: `Bearer ${secret}` };
    },
    credentials: () => 'omit',
  };
}
