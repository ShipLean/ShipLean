import { useQuery } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { useParams } from 'react-router-dom';
import type { ProjectDto } from '../api/projects';
import { useScreenPresence } from '../realtime';
import type { RouteId } from '../routes';
import { projectByKeyQuery } from './projectQueries';
import { QueryFailure } from './QueryFailure';

interface ProjectScreenProps {
  routeId: RouteId;
  testId: string;
  children: (project: ProjectDto) => ReactNode;
}

// The root of every project page: reports presence, resolves the project the URL's key names (not found when it does
// not exist or you are not a member) and hands it to the page's layout.
export function ProjectScreen({ routeId, testId, children }: ProjectScreenProps) {
  useScreenPresence(routeId);
  const { key = '' } = useParams();
  const project = useQuery(projectByKeyQuery(key));
  // A failed refetch keeps the data it has: the page stays up (its slots hold drafts and dialogs) and the failure
  // state is only for a page that never loaded.
  if (project.data === undefined) {
    return project.isError ? <QueryFailure error={project.error} /> : null;
  }
  return (
    <div data-testid={testId} className="p-4">
      {children(project.data)}
    </div>
  );
}
