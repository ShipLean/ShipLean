import { useMutation, useQueryClient } from '@tanstack/react-query';
import { putRetroAction } from '../../../api/retrosApi';
import type { RetroActionPut } from '../../../api/retros';
import { showToast } from '../../../components/toastStore';
import { retrosRootKey } from '../useRetroQueries';

/** PUT api/retros/actions/{id}: a full replacement (K5), so the caller sends both current values with the one it changed. Failure is a toast. */
export function useUpdateRetroAction(projectId: string, workItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (put: RetroActionPut) => putRetroAction(workItemId, put),
    onSettled: () => queryClient.invalidateQueries({ queryKey: retrosRootKey(projectId) }),
    onError: (failure: Error) => showToast(failure.message, { tone: 'danger' }),
  });
}
