import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  archiveRetroPoint,
  createRetroAction,
  putRetroPointTheme,
} from '../../../../../api/retrosApi';
import { showToast } from '../../../../../components/toastStore';
import { retrosRootKey } from '../../../useRetroQueries';

/** The three writes a By category point offers. Each refetches the project's retro reads when it settles (a 409 too), so the point shows what the server now says; a failure shows as a toast. */
export function useRetroPointWrites(projectId: string) {
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
  const addAction = useMutation({
    mutationFn: (pointId: string) => createRetroAction(pointId, { title: null, points: null }),
    onSettled: refresh,
    onError: notify,
  });
  const archive = useMutation({
    mutationFn: (pointId: string) => archiveRetroPoint(pointId),
    onSettled: refresh,
    onError: notify,
  });
  return { moveTheme, addAction, archive };
}
