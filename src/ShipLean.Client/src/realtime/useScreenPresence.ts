import { useEffect } from 'react';
import { isInstanceLevel, type RouteId } from '../routes';
import { realtime } from './realtime';

// Reports this screen as open while the caller is mounted. Install-level screens have no presence.
export function useScreenPresence(routeId: RouteId): void {
  useEffect(() => {
    if (isInstanceLevel(routeId)) return;
    realtime.setScreen(routeId);
    return () => realtime.setScreen(undefined);
  }, [routeId]);
}
