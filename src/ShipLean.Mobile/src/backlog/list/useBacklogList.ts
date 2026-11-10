import {
  backlogKeys,
  getBacklog,
  type BacklogFilter,
  type BacklogLevel,
} from '@shiplean/core/api/backlog';
import { skipToken, useQuery } from '@tanstack/react-query';

// One level's list-mode read. Waits (no request) while `filter` is null or the project id is unknown. It shares
// backlogKeys.level with useBacklogQuery, so a move's refresh reaches it too.
export function useBacklogList(
  projectId: string | undefined,
  level: BacklogLevel,
  filter: BacklogFilter | null,
) {
  return useQuery({
    queryKey: backlogKeys.level(
      projectId ?? '',
      level,
      filter ?? { mode: 'list', filter: '', includeCompleted: true },
    ),
    queryFn:
      projectId === undefined || filter === null
        ? skipToken
        : () => getBacklog(projectId, level, filter),
  });
}
