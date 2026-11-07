import { useEffect } from 'react';
import { postScreenView } from '../api/usageApi';
import type { BacklogMode, RouteId } from '../routes';

// Reports one screen view to the server when the caller mounts, and again when the route id, the project or the backlog mode
// changes (not on a re-render or a refetch). Fire and forget: a failed report is dropped, with no retry.
export function useScreenView(
  routeId: RouteId,
  projectId: string | null,
  mode?: BacklogMode,
): void {
  useEffect(() => {
    postScreenView(
      mode === undefined ? { screen: routeId, projectId } : { screen: routeId, projectId, mode },
    ).catch(() => undefined);
  }, [routeId, projectId, mode]);
}
