import { useParams } from 'react-router-dom';
import type { ProjectSummaryDto } from '../../api/projects';
import { useProjects, useRecentProjectIds } from './useShellQueries';

export interface ShellProject {
  // The project key the shell is showing: the route's on a project page, else the most recent project's (none when
  // nothing was opened yet).
  key: string | undefined;
  // The caller's membership of it; undefined while loading or when they are not a member.
  project: ProjectSummaryDto | undefined;
  // True on /p/:key/…, where the shell also subscribes to real-time updates.
  onProjectRoute: boolean;
}

export function useShellProject(): ShellProject {
  const routeKey = useParams().key;
  const projects = useProjects().data;
  const recent = useRecentProjectIds().data;
  const onProjectRoute = routeKey !== undefined;
  const project = onProjectRoute
    ? projects?.find((p) => p.key === routeKey)
    : projects?.find((p) => p.id === recent?.[0]);
  return { key: routeKey ?? project?.key, project, onProjectRoute };
}
