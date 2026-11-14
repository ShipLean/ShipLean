import type { QueryKey } from '@tanstack/react-query';
import { useQueryClient } from '@tanstack/react-query';
import type { BacklogRowDto, BacklogViewDto } from '@shiplean/core/api/views';
import { useReorder } from '../drag/useReorder';

/**
 * What a touch drop means (SHIP-716 D2): the dragged row goes just above the row it was dropped on, through the same
 * write, optimistic move, refetch and 409 conflict handling as the desktop drag (`useReorder.drop`). A drop on the row
 * itself, or on nothing, sends nothing.
 *
 * `drop(rowId, targetId)`: `targetId` is what TouchHandle's onDrop reports. The row's key and revision come from the
 * backlog read under `queryKey`. `conflict`, `useMine` and `keepTheirs` are useReorder's, for the screen's
 * ReorderConflict banner; a screen that already owns a useReorder must use this hook's instead of a second one, or
 * the conflict would sit in a state the banner doesn't read.
 */
export function useTouchReorder(projectId: string, queryKey: QueryKey) {
  const queryClient = useQueryClient();
  const reorder = useReorder(projectId, queryKey);

  function drop(rowId: string, targetId: string | null): void {
    if (targetId === null || targetId === rowId) return;
    const view = queryClient.getQueryData<BacklogViewDto>(queryKey);
    const rows: BacklogRowDto[] = [
      ...(view?.rows ?? []),
      ...(view?.sections ?? []).flatMap((s) => s.rows),
    ];
    const row = rows.find((candidate) => candidate.id === rowId);
    if (row === undefined) return;
    void reorder.drop({ id: row.id, key: row.key, revision: row.revision }, targetId);
  }

  return {
    drop,
    conflict: reorder.conflict,
    useMine: reorder.useMine,
    keepTheirs: reorder.keepTheirs,
  };
}
