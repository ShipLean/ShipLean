// The app's one session, bound to the phone's storage: the secret in expo-secure-store (Keychain or Keystore), the
// server and email in expo-sqlite's kv-store. Both read synchronously here, so the first render knows the gate's side.
import type { KeyValueStore } from '@shiplean/core/platform';
import * as SecureStore from 'expo-secure-store';
import Storage from 'expo-sqlite/kv-store';
import { DEFAULT_ORIGIN, createSessionStore } from './sessionStore';

// Also the platform's persistentStore.
export const kvStore: KeyValueStore = {
  getItem: (k) => Storage.getItemSync(k),
  setItem: (k, v) => Storage.setItemSync(k, v),
  removeItem: (k) => {
    Storage.removeItemSync(k);
  },
};

export const session = createSessionStore({
  secure: SecureStore,
  kv: kvStore,
  defaultOrigin: DEFAULT_ORIGIN,
});
