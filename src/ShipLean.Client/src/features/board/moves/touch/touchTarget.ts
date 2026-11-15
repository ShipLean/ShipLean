import type { DropTarget } from '../applyMove';

/** Where a finger let go: the drop and the element the drop line belongs to. */
export interface TouchTarget {
  to: DropTarget;
  /** The `data-drop-target` value of the card or cell that shows the line. */
  lineAt: string;
}

/**
 * Maps the `[data-drop-target]` element under a finger to a drop. A card is dropped above (in its cell); a cell at the
 * end. The dragged card itself, and nothing at all, are no move.
 */
export function touchTarget(element: HTMLElement | null, draggedId: string): TouchTarget | null {
  const lineAt = element?.dataset.dropTarget;
  if (!element || lineAt === undefined) return null;
  const cell = element.closest<HTMLElement>('[data-lane]');
  if (!cell) return null;
  const { lane, column, doneHalf } = cell.dataset;
  if (lane === undefined || column === undefined) return null;
  const isCell = element === cell;
  if (!isCell && lineAt === draggedId) return null;
  return {
    lineAt,
    to: {
      laneId: lane,
      columnId: column,
      done: doneHalf === 'true',
      beforeItemId: isCell ? null : lineAt,
    },
  };
}
