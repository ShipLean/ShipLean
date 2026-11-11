import type { ProjectSummaryDto } from '@shiplean/core/api/projects';
import { listProjects, projectsKey } from '@shiplean/core/api/projects';
import { useQuery } from '@tanstack/react-query';

export interface ProjectResult {
  project: ProjectSummaryDto | undefined;
  isLoading: boolean;
  error: Error | null;
}

// One project from the projects list Projects already loaded (the projectsKey query), by key.
export function useProject(projectKey: string): ProjectResult {
  const projects = useQuery({ queryKey: projectsKey, queryFn: listProjects });
  return {
    project: projects.data?.find((p) => p.key === projectKey),
    isLoading: projects.isLoading,
    error: projects.error,
  };
}
