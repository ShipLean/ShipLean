import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../api/client';
import type { VelocityOverridePut } from '../../../api/sprints';
import { backlogRootKey } from '../backlogApi';

/**
 * Sets (or with null clears) the project's velocity override, which any member may do. The forecast moves, so the
 * backlog read and the project (it carries the override) refetch.
 */
export function useVelocityOverride(projectId: string, projectKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (velocityOverride: number | null) =>
      api.put<void>(`/api/projects/${encodeURIComponent(projectId)}/forecast`, {
        velocityOverride,
      } satisfies VelocityOverridePut),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: backlogRootKey(projectId) }),
        queryClient.invalidateQueries({ queryKey: ['projects', 'by-key', projectKey] }),
      ]);
    },
  });
}
