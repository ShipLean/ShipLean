import { useQuery } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import { sprintsQuery } from './projectQueries';
import { QueryFailure } from './QueryFailure';

interface WithSprintsProps {
  project: ProjectDto;
  children: (sprints: SprintDto[]) => ReactNode;
}

// Reads the project's sprints (the shared query) for the part of a page that needs them.
export function WithSprints({ project, children }: WithSprintsProps) {
  const sprints = useQuery(sprintsQuery(project.id));
  // As in ProjectScreen: the failure state only while there is no data to show.
  if (sprints.data === undefined) {
    return sprints.isError ? <QueryFailure error={sprints.error} /> : null;
  }
  return <>{children(sprints.data)}</>;
}
