import { useSyncExternalStore } from 'react';
import type { ConnectionState } from './connectionState';
import { realtime } from './realtime';

export function useConnectionState(): ConnectionState {
  return useSyncExternalStore(realtime.connectionState.subscribe, realtime.connectionState.get);
}
