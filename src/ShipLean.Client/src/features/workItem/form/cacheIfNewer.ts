import type { QueryClient } from '@tanstack/react-query';
import type { WorkItemDto } from '../../../api/workItems';
import { workItemKey } from '../workItemApi';

// Puts a revision the server sent into the item's cache unless the cache already holds that revision or a newer one (an
// event refetch may have beaten the conflict body to it, and going back would make the next Save conflict again).
export function cacheIfNewer(queryClient: QueryClient, current: WorkItemDto) {
  const key = workItemKey(current.projectId, current.key);
  const cached = queryClient.getQueryData<WorkItemDto>(key);
  if (cached === undefined || cached.revision < current.revision) {
    queryClient.setQueryData(key, current);
  }
}
