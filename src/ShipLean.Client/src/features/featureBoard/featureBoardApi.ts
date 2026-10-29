import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo } from 'react';
import { api } from '../../api/client';
import type { FeatureBoardViewDto } from '../../api/views';
import { registerInvalidation } from '../../realtime';
import { taskboardKey, taskboardMoveKey, TASKBOARD_EVENTS } from '../taskboard/taskboardApi';
import type { TaskboardScope } from '../taskboard/taskboardScope';

export const featureBoardPath = (projectId: string, itemKey: string): string =>
  `/api/projects/${projectId}/work-items/by-key/${encodeURIComponent(itemKey)}/feature-board`;

// The page only knows the Feature's key, so the first read is keyed by it. Once the view says which Feature it is, the
// view lives under the scope's key (taskboardKey), the key every board write invalidates.
const byKeyKey = (projectId: string, itemKey: string) =>
  ['projects', projectId, 'feature-board', 'by-key', itemKey] as const;

/**
 * The Feature board read model for the Feature the key names: one GET, then refetched under the Feature scope's key on
 * the Taskboard's events (not while a move is in flight; the last move to settle refetches). `scope` is undefined until
 * the first read has answered. A key that is not a live Feature fails with `not_found`.
 */
export function useFeatureBoard(projectId: string, itemKey: string) {
  const queryClient = useQueryClient();
  const first = useQuery({
    queryKey: byKeyKey(projectId, itemKey),
    queryFn: () => api.get<FeatureBoardViewDto>(featureBoardPath(projectId, itemKey)),
    staleTime: Infinity,
    gcTime: 0,
  });
  const featureId = first.data?.featureId;
  const scope = useMemo<TaskboardScope | undefined>(
    () => (featureId === undefined ? undefined : { kind: 'feature', featureId }),
    [featureId],
  );
  const board = useQuery({
    queryKey: scope
      ? taskboardKey(projectId, scope)
      : (['projects', projectId, 'feature-board', 'pending'] as const),
    queryFn: () => api.get<FeatureBoardViewDto>(featureBoardPath(projectId, itemKey)),
    enabled: scope !== undefined,
    initialData: first.data,
    initialDataUpdatedAt: first.dataUpdatedAt,
    // The first read just answered: no second one on mount.
    staleTime: 10_000,
  });
  useEffect(() => {
    if (!scope) return;
    const refetch = () => {
      // A refetch now would show the board without the move still on its way.
      if (queryClient.isMutating({ mutationKey: taskboardMoveKey(projectId, scope) }) > 0) return;
      void queryClient.invalidateQueries({ queryKey: taskboardKey(projectId, scope) });
    };
    const unregister = TASKBOARD_EVENTS.map((type) => registerInvalidation(type, refetch));
    return () => unregister.forEach((off) => off());
  }, [projectId, scope, queryClient]);
  return { query: scope ? board : first, scope };
}
