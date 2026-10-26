import { arrayMove } from '@dnd-kit/sortable';

export interface ReorderMove {
  /** The rows in their new order. */
  ids: string[];
  /** The item the moved one now sits just above, or null when it is last. */
  beforeItemId: string | null;
}

/** The new order after dropping `activeId` where `overId` is, or null when nothing moves. */
export function reorderTarget(
  ids: readonly string[],
  activeId: string,
  overId: string,
): ReorderMove | null {
  const from = ids.indexOf(activeId);
  const to = ids.indexOf(overId);
  if (from < 0 || to < 0 || from === to) return null;
  const next = arrayMove([...ids], from, to);
  return { ids: next, beforeItemId: next[next.indexOf(activeId) + 1] ?? null };
}
