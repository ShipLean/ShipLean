import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import type { BoardViewDto } from '../../../../api/views';
import { registerInvalidation } from '../../../../realtime';
import { boardKey, boardRootKey, fetchBoard } from '../../../board/boardApi';

const countsOf = (board: BoardViewDto): ReadonlyMap<string, number> =>
  new Map(board.columns.map((c) => [c.id, c.count]));

/**
 * `countNow(columnId)` says how many cards a column holds from the unfiltered board (so the counts cover every lane and
 * both halves of a split column), the same query the board page reads. It waits only while that read is still loading,
 * and gives undefined when the board can't say (it failed, or this is the Taskboard). A column the board doesn't list
 * holds none. The query refetches on the events that move cards.
 */
export function useColumnCounts(projectId: string, boardId: string, enabled: boolean) {
  const queryClient = useQueryClient();
  const key = boardKey(projectId, boardId, {});
  const queryFn = () => fetchBoard(boardId, {});
  useQuery({ queryKey: key, enabled, queryFn });
  useEffect(() => {
    if (!enabled) return undefined;
    const refetch = () =>
      void queryClient.invalidateQueries({ queryKey: boardRootKey(projectId, boardId) });
    const off = [
      registerInvalidation('workItemCreated', refetch),
      registerInvalidation('workItemUpdated', refetch),
      registerInvalidation('workItemMoved', refetch),
      registerInvalidation('workItemRemoved', refetch),
      registerInvalidation('boardSettingsChanged', refetch),
      registerInvalidation('statesChanged', refetch),
    ];
    return () => off.forEach((stop) => stop());
  }, [enabled, projectId, boardId, queryClient]);
  return async (columnId: string): Promise<number | undefined> => {
    if (!enabled) return undefined;
    try {
      const board: BoardViewDto =
        queryClient.getQueryData<BoardViewDto>(key) ??
        (await queryClient.ensureQueryData<BoardViewDto>({ queryKey: key, queryFn }));
      return countsOf(board).get(columnId) ?? 0;
    } catch {
      return undefined;
    }
  };
}
