import { useEffect, useRef } from 'react';
import type { CardDto } from '@shiplean/core/api/views';
import { useLongPressDrag, type LongPressDrag } from '../../../touch/useLongPressDrag';
import { useTouchShell } from '../../../touch/useTouchShell';
import { useTaskboardPager } from '../grid/phone/TaskboardPagerContext';
import type { TaskboardGrouping } from '../taskboardApi';
import { useTaskboardMoves } from './TaskboardMovesContext';
import type { DropTarget } from './moveRequests';

/** A lane's task area carries `data-row-id` and `data-state-id`: where a drop on it puts the card. Null when nothing is under the finger or the area is incomplete. */
export function dropTargetOf(target: HTMLElement | null): Omit<DropTarget, 'beforeItemId'> | null {
  const rowId = target?.dataset.rowId;
  const stateId = target?.dataset.stateId;
  return rowId && stateId ? { rowId, stateId } : null;
}

interface RowsOf {
  cells: { tasks: Pick<CardDto, 'id' | 'key'>[] }[];
}

/** The card a dropped card goes above: only Stories cells keep an order, and a card dropped on itself goes above nothing. */
export function beforeItemIdAt(
  grouping: TaskboardGrouping,
  view: { rows: RowsOf[] },
  cardKey: string | null,
  ownId: string,
): string | null {
  if (grouping !== 'stories' || cardKey === null) return null;
  const cells = view.rows.flatMap((row) => row.cells);
  const over = cells.flatMap((cell) => cell.tasks).find((task) => task.key === cardKey);
  return over && over.id !== ownId ? over.id : null;
}

/** Holding at the left edge turns to the previous state's page, at the right edge to the next. */
export const pagerStep = (dir: 'left' | 'right'): -1 | 1 => (dir === 'left' ? -1 : 1);

function cardKeyAt(x: number, y: number): string | null {
  return (
    document.elementFromPoint(x, y)?.closest<HTMLElement>('[data-key]')?.getAttribute('data-key') ??
    null
  );
}

/**
 * A task card that lifts after a long press on the phone grid (K6): the drop goes through the board's moves like a mouse drop,
 * recorded as `touch-drag`. A release without moving opens the card's actions (`onActions`). `enabled` is false above 900px and
 * outside the phone grid, where dnd-kit moves cards instead.
 */
export function useTouchCardDrag(
  card: CardDto,
  rowId: string,
  onActions: () => void,
): { enabled: boolean; drag: LongPressDrag } {
  const { touch } = useTouchShell();
  const pager = useTaskboardPager();
  const moves = useTaskboardMoves();
  const active = useRef<HTMLElement | null>(null);
  const mark = (next: HTMLElement | null) => {
    if (active.current === next) return;
    active.current?.removeAttribute('data-drop-active');
    next?.setAttribute('data-drop-active', '');
    active.current = next;
  };
  const drag = useLongPressDrag({
    onMove: (target) => mark(dropTargetOf(target) ? target : null),
    onDrop: (target, point) => {
      mark(null);
      const to = dropTargetOf(target);
      if (!to) return;
      const beforeItemId = beforeItemIdAt(
        moves.grouping,
        moves.view,
        cardKeyAt(point.x, point.y),
        card.id,
      );
      moves.drop(card, rowId, { ...to, beforeItemId }, 'touch-drag');
    },
    onTapWithoutMove: onActions,
    onEdge: (dir) => {
      mark(null);
      pager?.turn(pagerStep(dir));
    },
  });
  // A cancelled touch (or the card going away) lets go without a drop: the lane must not stay highlighted.
  const { lifted } = drag;
  useEffect(() => {
    if (lifted) return;
    active.current?.removeAttribute('data-drop-active');
    active.current = null;
  }, [lifted]);
  return { enabled: touch && pager !== null, drag };
}
