import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { RetroPointCreate } from '../../../api/retros';
import { createRetroPoint } from '../../../api/retrosApi';
import { retrosRootKey } from '../../retros/useRetroQueries';

/** POST api/projects/{id}/retros/points, then refetch the project's retro reads. */
export function useCreatePoint(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (create: RetroPointCreate) => createRetroPoint(projectId, create),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: retrosRootKey(projectId) }),
  });
}
