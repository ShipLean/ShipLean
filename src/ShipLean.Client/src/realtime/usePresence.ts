import type { RouteId } from '../routes';
import { withoutMe } from './presence';
import { useMeId } from './useMeId';
import { useScreenUserIds } from './useScreenUserIds';

// User ids on this screen other than me.
export function usePresence(routeId: RouteId): string[] {
  return withoutMe(useScreenUserIds(routeId), useMeId());
}
