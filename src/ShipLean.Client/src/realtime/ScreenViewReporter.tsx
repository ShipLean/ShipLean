import { useRouteQuery } from '../pages/useRouteQuery';
import type { BacklogMode, RouteId } from '../routes';
import { useScreenView } from './useScreenView';

// The mode a backlog screen shows (as its slots read it); other screens have none. Epics are always a list, and Features have
// no Planning mode, so it shows List.
function backlogMode(routeId: RouteId, mode: BacklogMode | undefined): BacklogMode | undefined {
  if (routeId === 'storiesBacklog') return mode ?? 'list';
  if (routeId === 'featuresBacklog') return mode === 'tree' ? 'tree' : 'list';
  if (routeId === 'epicsBacklog') return 'list';
  return undefined;
}

interface ScreenViewReporterProps {
  routeId: RouteId;
  projectId: string;
}

// Reports a project screen view once the project is known, with the mode on a backlog; renders nothing.
export function ScreenViewReporter({ routeId, projectId }: ScreenViewReporterProps) {
  const mode = backlogMode(routeId, useRouteQuery().mode);
  // Only a backlog passes a mode, so every other screen reports exactly as before.
  const args: Parameters<typeof useScreenView> =
    mode === undefined ? [routeId, projectId] : [routeId, projectId, mode];
  useScreenView(...args);
  return null;
}
