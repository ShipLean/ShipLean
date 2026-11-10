import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@shiplean/core/api/client';
import type {
  BacklogAddRequest,
  BacklogAddResult,
  DuplicateCheckRequest,
  DuplicateCheckResult,
} from '@shiplean/core/api/backlogWrites';
import { backlogRootKey } from '../../../queries/backlogKeys';

/** Creates many rows (K2) and reads the backlog and the project's counters again, as useCreateWorkItem does. */
export function useBacklogAdd(projectId: string, projectKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: BacklogAddRequest) =>
      api.post<BacklogAddResult>(
        `/api/projects/${encodeURIComponent(projectId)}/backlog/items`,
        request,
      ),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: backlogRootKey(projectId) }),
        queryClient.invalidateQueries({ queryKey: ['projects', 'by-key', projectKey] }),
      ]);
    },
  });
}

/** Asks which titles look like existing stories or bugs (K3). Read-only. */
export function useDuplicateCheck(projectId: string) {
  return useMutation({
    mutationFn: (request: DuplicateCheckRequest) =>
      api.post<DuplicateCheckResult>(
        `/api/projects/${encodeURIComponent(projectId)}/backlog/duplicates`,
        request,
      ),
  });
}
