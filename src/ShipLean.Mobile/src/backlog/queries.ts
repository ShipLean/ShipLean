import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  backlogKeys,
  getBacklog,
  type BacklogFilter,
  type BacklogLevel,
} from '@shiplean/core/api/backlog';

export function useBacklogQuery(projectId: string, level: BacklogLevel, filter: BacklogFilter) {
  return useQuery({
    queryKey: backlogKeys.level(projectId, level, filter),
    queryFn: () => getBacklog(projectId, level, filter),
  });
}

// Every level and mode of the project's backlog.
export function useInvalidateBacklog() {
  const client = useQueryClient();
  return (projectId: string) => client.invalidateQueries({ queryKey: backlogKeys.root(projectId) });
}
