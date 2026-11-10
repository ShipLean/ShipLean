import { getMembers, projectMetaKeys } from '@shiplean/core/api/projectMeta';
import { skipToken, useQuery } from '@tanstack/react-query';

// The project's people, for the voice context. Shares the cache key every screen's member list reads.
export function useFeatureMembers(projectId: string | undefined) {
  return useQuery({
    queryKey: projectMetaKeys.members(projectId ?? ''),
    queryFn: projectId === undefined || projectId === '' ? skipToken : () => getMembers(projectId),
  });
}
