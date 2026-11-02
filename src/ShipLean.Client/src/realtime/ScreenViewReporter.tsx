import type { RouteId } from '../routes';
import { useScreenView } from './useScreenView';

interface ScreenViewReporterProps {
  routeId: RouteId;
  projectId: string;
}

// Reports a project screen view once the project is known; renders nothing.
export function ScreenViewReporter({ routeId, projectId }: ScreenViewReporterProps) {
  useScreenView(routeId, projectId);
  return null;
}
