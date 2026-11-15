import type { BoardViewDto } from '@shiplean/core/api/views';
import { tracksOf } from '../../grid/layout';
import { locate, type DropTarget } from '../../moves/applyMove';

/** One place the "Move to" sheet offers: a column, or one half of a split column. */
export interface MoveTarget {
  columnId: string;
  done: boolean;
  /** "In progress", or "Review · Doing" / "Review · Done" for a split column. */
  label: string;
  /** "2 of 3" when the column has a WIP limit, else the count alone. */
  wip: string;
  /** The card is already here. */
  current: boolean;
}

/** Every column of the board, a split one twice, in board order, with where the card stands marked. */
export function moveTargets(view: BoardViewDto, itemId: string): MoveTarget[] {
  const at = locate(view, itemId);
  return tracksOf(view.columns).map(({ column, doneHalf }) => {
    const count = column.split ? (doneHalf ? column.doneCount : column.doingCount) : column.count;
    return {
      columnId: column.id,
      done: doneHalf,
      label: column.split ? `${column.name} · ${doneHalf ? 'Done' : 'Doing'}` : column.name,
      wip:
        column.wipLimit === null
          ? `${count}`
          : `${column.count} of ${column.wipLimit}${column.overLimit ? ' (over)' : ''}`,
      current: at?.columnId === column.id && at.done === doneHalf,
    };
  });
}

/** Move to: the end of that column in the card's own lane, as Alt + an arrow does. */
export function dropAtEnd(
  view: BoardViewDto,
  itemId: string,
  target: Pick<MoveTarget, 'columnId' | 'done'>,
): DropTarget | null {
  const at = locate(view, itemId);
  return at
    ? { laneId: at.laneId, columnId: target.columnId, done: target.done, beforeItemId: null }
    : null;
}

/** The lanes a card can go to besides its own: ones whose criteria accept a drop. */
export function otherLanes(view: BoardViewDto, itemId: string) {
  const at = locate(view, itemId);
  return view.lanes.filter((lane) => lane.id !== at?.laneId && lane.acceptsDrops);
}

/** Move to another lane: same column and half, the end of that cell. */
export function dropInLane(view: BoardViewDto, itemId: string, laneId: string): DropTarget | null {
  const at = locate(view, itemId);
  return at ? { laneId, columnId: at.columnId, done: at.done, beforeItemId: null } : null;
}

/** Move to top: above the first card of its cell; null when it already is. */
export function dropAtTop(view: BoardViewDto, itemId: string): DropTarget | null {
  const at = locate(view, itemId);
  const first = at?.cards[0];
  if (!at || !first || first.id === itemId) return null;
  return { laneId: at.laneId, columnId: at.columnId, done: at.done, beforeItemId: first.id };
}

/** Move to bottom: the end of its cell; null when it already is. */
export function dropAtBottom(view: BoardViewDto, itemId: string): DropTarget | null {
  const at = locate(view, itemId);
  if (!at || at.index === at.cards.length - 1) return null;
  return { laneId: at.laneId, columnId: at.columnId, done: at.done, beforeItemId: null };
}
