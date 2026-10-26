import { useParams } from 'react-router-dom';
import type { ReactNode } from 'react';
import type { ProjectDto } from '../api/projects';
import type { SprintDto } from '../api/sprints';
import type { RouteId } from '../routes';
import { NotFoundState } from './NotFoundState';
import { ProjectScreen } from './ProjectScreen';
import { WithSprints } from './WithSprints';

interface SprintScreenProps {
  routeId: RouteId;
  testId: string;
  children: (project: ProjectDto, sprint: SprintDto, sprints: SprintDto[]) => ReactNode;
}

// A project page that is about one sprint: also resolves the sprint the URL names from the project's sprints (not
// found when the project has no such sprint).
export function SprintScreen({ routeId, testId, children }: SprintScreenProps) {
  const { sprintId } = useParams();
  return (
    <ProjectScreen routeId={routeId} testId={testId}>
      {(project) => (
        <WithSprints project={project}>
          {(sprints) => {
            const sprint = sprints.find((s) => s.id === sprintId);
            return sprint === undefined ? <NotFoundState /> : children(project, sprint, sprints);
          }}
        </WithSprints>
      )}
    </ProjectScreen>
  );
}
