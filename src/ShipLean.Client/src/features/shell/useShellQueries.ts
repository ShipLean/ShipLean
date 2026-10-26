import { useQuery } from '@tanstack/react-query';
import {
  fetchMe,
  fetchProjects,
  fetchSprints,
  meKey,
  projectsKey,
  projectSprintsKey,
} from '../../queries/shared';
import { fetchRecentProjectIds, recentProjectsKey } from './shellApi';

export const useMe = () => useQuery({ queryKey: meKey, queryFn: fetchMe });
export const useProjects = () => useQuery({ queryKey: projectsKey, queryFn: fetchProjects });
export const useRecentProjectIds = () =>
  useQuery({ queryKey: recentProjectsKey, queryFn: fetchRecentProjectIds });
export const useSprints = (projectId: string | undefined, enabled: boolean) =>
  useQuery({
    queryKey: projectSprintsKey(projectId ?? ''),
    queryFn: () => fetchSprints(projectId ?? ''),
    enabled: enabled && projectId !== undefined,
  });
