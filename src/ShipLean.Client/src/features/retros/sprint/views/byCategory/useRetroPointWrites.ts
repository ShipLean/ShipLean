import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  archiveRetroPoint,
  createRetroAction,
  putRetroPointTheme,
} from '../../../../../api/retrosApi';
import { retrosRootKey } from '../../../useRetroQueries';

/** The three writes a By category point offers. Each refetches the project's retro reads, so the point shows what the server now says. */
export function useRetroPointWrites(projectId: string) {
  const queryClient = useQueryClient();
  const refresh = () => queryClient.invalidateQueries({ queryKey: retrosRootKey(projectId) });
  const moveTheme = useMutation({
    mutationFn: (v: { pointId: string; pick: string | { name: string } }) =>
      putRetroPointTheme(
        v.pointId,
        typeof v.pick === 'string'
          ? { themeId: v.pick, themeName: null }
          : { themeId: null, themeName: v.pick.name },
      ),
    onSuccess: refresh,
  });
  const addAction = useMutation({
    mutationFn: (pointId: string) => createRetroAction(pointId, { title: null, points: null }),
    onSuccess: refresh,
  });
  const archive = useMutation({
    mutationFn: (pointId: string) => archiveRetroPoint(pointId),
    onSuccess: refresh,
  });
  return { moveTheme, addAction, archive };
}
