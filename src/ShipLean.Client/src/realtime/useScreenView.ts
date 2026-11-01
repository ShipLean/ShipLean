import type { RouteId } from '../routes';

// Reports one screen view to the server when the caller mounts (or the route or project changes).
// TODO: post the view through postScreenView once the usage endpoint records it; until then this does nothing.
export function useScreenView(routeId: RouteId, projectId: string | null): void {
  void routeId;
  void projectId;
}
