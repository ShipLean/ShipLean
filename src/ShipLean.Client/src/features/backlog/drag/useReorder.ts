import { useState } from 'react';
import { useQueryClient, type QueryKey } from '@tanstack/react-query';
import { api } from '../../../api/client';
import { conflictOf, type ReorderRequest } from '../../../api/workItems';
import type { BacklogViewDto } from '../../../api/views';
import { showToast } from '../../../components/toastStore';
import { backlogRootKey } from '../backlogApi';

/** A write against an item at a revision: the reorder POST, a reparent, a sprint move. A stale revision answers 409. */
export type RevisionWrite = (revision: number) => Promise<unknown>;

/** The write for a plain reorder of one item. */
export const reorderWrite =
  (itemId: string, beforeItemId: string | null): RevisionWrite =>
  (expectedRevision) =>
    api.post(`/api/work-items/${encodeURIComponent(itemId)}/reorder`, {
      expectedRevision,
      beforeItemId,
    } satisfies ReorderRequest);

export interface ReorderConflict {
  itemId: string;
  itemKey: string;
  changedBy: string;
  /** The revision the item is at now. */
  currentRevision: number;
  /** Sends the same write again against another revision (Use mine sends the current one). */
  resend: RevisionWrite;
}

/**
 * Where the moved row goes in a read of the backlog: just above `beforeItemId`, or last. Only that row moves; rows
 * that are not items (group rows) keep their places.
 */
export function moveRow(
  view: BacklogViewDto,
  itemId: string,
  beforeItemId: string | null,
): BacklogViewDto {
  const moved = view.rows.find((row) => row.id === itemId);
  if (moved === undefined) return view;
  const rest = view.rows.filter((row) => row.id !== itemId);
  const at = beforeItemId === null ? -1 : rest.findIndex((row) => row.id === beforeItemId);
  return {
    ...view,
    rows: at < 0 ? [...rest, moved] : [...rest.slice(0, at), moved, ...rest.slice(at)],
  };
}

/**
 * The section-aware move for Planning: the row leaves its section and goes into `sectionId`, just above
 * `beforeItemId` or last, with that section's sprint. Both sections' points follow the row.
 */
export function moveRowToSection(
  view: BacklogViewDto,
  itemId: string,
  sectionId: string,
  beforeItemId: string | null,
): BacklogViewDto {
  const source = view.sections.find((s) => s.rows.some((row) => row.id === itemId));
  const target = view.sections.find((s) => s.id === sectionId);
  const moved = source?.rows.find((row) => row.id === itemId);
  if (source === undefined || target === undefined || moved === undefined) return view;
  const points = moved.points ?? 0;
  const placed = { ...moved, sprint: target.sprint };
  return {
    ...view,
    sections: view.sections.map((section) => {
      const rest = section.rows.filter((row) => row.id !== itemId);
      if (section.id !== target.id) {
        return section.id === source.id
          ? { ...section, rows: rest, points: section.points - points }
          : section;
      }
      const at = beforeItemId === null ? -1 : rest.findIndex((row) => row.id === beforeItemId);
      return {
        ...section,
        rows: at < 0 ? [...rest, placed] : [...rest.slice(0, at), placed, ...rest.slice(at)],
        points: source.id === target.id ? section.points : section.points + points,
      };
    }),
  };
}

/**
 * Sends a drop to the server through the write the caller passes. The row moves at once; the backlog is read again
 * after the answer (the ranks change). A stale revision's 409 keeps the drop as a conflict the user settles.
 */
export function useReorder(projectId: string, queryKey: QueryKey) {
  const queryClient = useQueryClient();
  const [conflict, setConflict] = useState<ReorderConflict | null>(null);
  const refetch = () => queryClient.invalidateQueries({ queryKey: backlogRootKey(projectId) });

  async function send(
    item: { id: string; key: string },
    write: RevisionWrite,
    revision: number,
    done: string,
  ): Promise<void> {
    try {
      await write(revision);
      // Only this item's own banner goes: another move's conflict is still waiting to be settled.
      setConflict((current) => (current?.itemId === item.id ? null : current));
      showToast(done);
    } catch (error) {
      const stale = conflictOf(error);
      if (stale) {
        setConflict({
          itemId: item.id,
          itemKey: item.key,
          changedBy:
            stale.theirChanges[0]?.changedBy?.name ?? stale.current.changedBy?.name ?? 'Someone',
          currentRevision: stale.current.revision,
          resend: (next) => send(item, write, next, done),
        });
      } else {
        showToast(error instanceof Error ? error.message : 'Could not move the item', {
          tone: 'danger',
        });
      }
    }
    await refetch();
  }

  function drop(
    item: { id: string; key: string; revision: number },
    beforeItemId: string | null,
    write: RevisionWrite = reorderWrite(item.id, beforeItemId),
    done = `${item.key} reordered`,
    place: (view: BacklogViewDto) => BacklogViewDto = (view) =>
      moveRow(view, item.id, beforeItemId),
  ) {
    queryClient.setQueryData<BacklogViewDto>(queryKey, (view) =>
      view === undefined ? view : place(view),
    );
    return send(item, write, item.revision, done);
  }

  return {
    conflict,
    drop,
    useMine: () => {
      if (conflict === null) return Promise.resolve();
      return conflict.resend(conflict.currentRevision);
    },
    keepTheirs: () => {
      setConflict(null);
      return refetch();
    },
  };
}
