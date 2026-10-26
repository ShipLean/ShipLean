import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { api } from '../../../../api/client';
import { registerInvalidation } from '../../../../realtime';

export const projectTagsKey = (projectId: string) => ['projects', projectId, 'tags'] as const;

/** Every tag on a live work item of the project, sorted by the server; refetched when someone creates, edits or removes an item. */
export function useProjectTags(projectId: string) {
  const queryClient = useQueryClient();
  const query = useQuery({
    queryKey: projectTagsKey(projectId),
    queryFn: () => api.get<string[]>(`/api/projects/${encodeURIComponent(projectId)}/tags`),
  });
  useEffect(() => {
    const refetch = () =>
      void queryClient.invalidateQueries({ queryKey: projectTagsKey(projectId) });
    const off = [
      registerInvalidation('workItemCreated', refetch),
      registerInvalidation('workItemUpdated', refetch),
      registerInvalidation('workItemRemoved', refetch),
    ];
    return () => off.forEach((stop) => stop());
  }, [projectId, queryClient]);
  return query;
}
