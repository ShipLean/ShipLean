import { useSyncExternalStore } from 'react';
import { session } from './session';
import type { SessionState } from './sessionStore';

export function useSession(): SessionState {
  return useSyncExternalStore(session.subscribe, session.get);
}
