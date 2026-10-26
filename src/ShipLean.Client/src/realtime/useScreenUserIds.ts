import { useSyncExternalStore } from 'react';
import type { RouteId } from '../routes';
import { realtime } from './realtime';

const NOBODY: readonly string[] = [];

// Everyone the hub last listed on a screen of the open project, me included.
export function useScreenUserIds(routeId: RouteId): readonly string[] {
  return useSyncExternalStore(
    realtime.presence.subscribe,
    () => realtime.presence.get().get(routeId) ?? NOBODY,
  );
}
