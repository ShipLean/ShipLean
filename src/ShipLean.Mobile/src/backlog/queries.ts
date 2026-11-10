import { getMembers, projectMetaKeys } from '@shiplean/core/api/projectMeta';
import { skipToken, useQuery, useQueryClient } from '@tanstack/react-query';
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

// The project's people, for the voice context. Shares the cache key every screen's member list reads.
export function useBacklogMembers(projectId: string | undefined) {
  return useQuery({
    queryKey: projectMetaKeys.members(projectId ?? ''),
    queryFn: projectId === undefined ? skipToken : () => getMembers(projectId),
  });
}
