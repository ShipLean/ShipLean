import type { BoardViewDto } from '../../../api/views';
import type { BoardMoveRequest } from '../boardApi';
import { tracksOf } from '../grid/layout';
import { locate, type DropTarget } from './applyMove';

/** What the board sends for a move; the revision is read when the request goes out. */
export type MoveBody = Omit<BoardMoveRequest, 'expectedRevision'>;

export const bodyOf = (itemId: string, to: DropTarget): MoveBody => ({
  itemId,
  columnId: to.columnId,
  done: to.done,
  laneId: to.laneId,
  beforeItemId: to.beforeItemId,
});

/** True when the drop would leave the card where it is, in the same order. */
export function staysPut(view: BoardViewDto, itemId: string, to: DropTarget): boolean {
  const at = locate(view, itemId);
  if (!at) return true;
  if (at.laneId !== to.laneId || at.columnId !== to.columnId || at.done !== to.done) return false;
  const next = at.cards[at.index + 1]?.id ?? null;
  return to.beforeItemId === itemId || to.beforeItemId === next;
}

/**
 * Where Alt + an arrow key sends a card: left and right to the next track (a split column has two) at the end of its
 * cell, up and down one place within its cell. Null at an edge or for any other key.
 */
export function keyboardTarget(view: BoardViewDto, itemId: string, key: string): DropTarget | null {
  const at = locate(view, itemId);
  if (!at) return null;
  if (key === 'ArrowLeft' || key === 'ArrowRight') {
    const tracks = tracksOf(view.columns);
    const index = tracks.findIndex(
      (track) => track.column.id === at.columnId && track.doneHalf === at.done,
    );
    const next = tracks[index + (key === 'ArrowLeft' ? -1 : 1)];
    return index < 0 || !next
      ? null
      : { laneId: at.laneId, columnId: next.column.id, done: next.doneHalf, beforeItemId: null };
  }
  if (key === 'ArrowUp') {
    const above = at.cards[at.index - 1];
    return above
      ? { laneId: at.laneId, columnId: at.columnId, done: at.done, beforeItemId: above.id }
      : null;
  }
  if (key === 'ArrowDown') {
    if (at.index + 1 >= at.cards.length) return null;
    const after = at.cards[at.index + 2]?.id ?? null;
    return { laneId: at.laneId, columnId: at.columnId, done: at.done, beforeItemId: after };
  }
  return null;
}
