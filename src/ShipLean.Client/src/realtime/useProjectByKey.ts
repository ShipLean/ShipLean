import { useQuery } from '@tanstack/react-query';
import { fetchProjectByKey, projectByKeyKey } from '../queries/shared';

// The shared ['projects', 'by-key', key] query: GET /api/projects/by-key/{key}.
export function useProjectByKey(projectKey: string | undefined) {
  return useQuery({
    queryKey: projectByKeyKey(projectKey ?? ''),
    queryFn: () => fetchProjectByKey(projectKey ?? ''),
    enabled: projectKey !== undefined,
  });
}
