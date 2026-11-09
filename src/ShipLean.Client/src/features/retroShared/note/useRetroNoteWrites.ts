import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  archiveRetroPoint,
  markPointNotNeeded,
  putRetroPointTheme,
  undoPointNotNeeded,
} from '@shiplean/core/api/retrosApi';
import { showToast } from '../../../components/toastStore';
import { retrosRootKey } from '../../retros/useRetroQueries';

/** The writes a note offers. Each refetches the project's retro reads when it settles (a 409 too); a failure shows as a toast. */
export function useRetroNoteWrites(projectId: string) {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: retrosRootKey(projectId) });
  const notify = (failure: Error) => showToast(failure.message, { tone: 'danger' });
  const moveTheme = useMutation({
    mutationFn: (v: { pointId: string; pick: string | { name: string } }) =>
      putRetroPointTheme(
        v.pointId,
        typeof v.pick === 'string'
          ? { themeId: v.pick, themeName: null }
          : { themeId: null, themeName: v.pick.name },
      ),
    onSettled: refresh,
    onError: notify,
  });
  const archive = useMutation({
    mutationFn: (pointId: string) => archiveRetroPoint(pointId),
    onSettled: refresh,
    onError: notify,
  });
  const notNeeded = useMutation({
    mutationFn: (v: { pointId: string; reason: string }) =>
      markPointNotNeeded(v.pointId, { reason: v.reason }),
    onSettled: refresh,
    onError: notify,
  });
  const undoNotNeeded = useMutation({
    mutationFn: (pointId: string) => undoPointNotNeeded(pointId),
    onSettled: refresh,
    onError: notify,
  });
  return { moveTheme, archive, notNeeded, undoNotNeeded };
}

export type RetroNoteWrites = ReturnType<typeof useRetroNoteWrites>;
