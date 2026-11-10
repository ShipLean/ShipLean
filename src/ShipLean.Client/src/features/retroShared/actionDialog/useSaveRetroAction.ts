import { useMutation, useQueryClient } from '@tanstack/react-query';
import { addPointsToRetroAction, newRetroAction } from '@shiplean/core/api/retrosApi';
import type { RetroActionNew } from '@shiplean/core/api/retros';
import { showToast } from '../../../components/toastStore';
import { retrosRootKey } from '../../retros/useRetroQueries';

export type SaveRetroAction =
  | { mode: 'new'; body: RetroActionNew }
  | { mode: 'existing'; workItemId: string; pointIds: string[] };

/**
 * Creates an action item over the checked points, or adds them to an open one. Every retro read refetches when it settles,
 * a 409 (a point got decided meanwhile) too, so the dialog shows what is still open; a failure shows as a toast.
 */
export function useSaveRetroAction(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (save: SaveRetroAction) =>
      save.mode === 'new'
        ? newRetroAction(projectId, save.body)
        : addPointsToRetroAction(save.workItemId, { pointIds: save.pointIds }),
    onSuccess: (detail, save) => {
      const count = save.mode === 'new' ? save.body.pointIds.length : save.pointIds.length;
      showToast(
        `${detail.action.key} covers ${detail.action.pointCount} point${detail.action.pointCount === 1 ? '' : 's'}.${save.mode === 'new' ? ' In New, no sprint.' : ` Added ${count}.`}`,
      );
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: retrosRootKey(projectId) }),
    onError: (failure: Error) => showToast(failure.message, { tone: 'danger' }),
  });
}
