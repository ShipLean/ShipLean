import { useEffect } from 'react';
import { postScreenView } from '../api/usageApi';
import type { RouteId } from '../routes';

// Reports one screen view to the server when the caller mounts, and again when the route id or the project changes (not on a
// re-render or a refetch). Fire and forget: a failed report is dropped, with no retry.
export function useScreenView(routeId: RouteId, projectId: string | null): void {
  useEffect(() => {
    postScreenView({ screen: routeId, projectId }).catch(() => undefined);
  }, [routeId, projectId]);
}
