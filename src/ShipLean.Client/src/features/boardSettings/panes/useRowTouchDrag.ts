import { useEffect, useEffectEvent, useRef } from 'react';
import { useLongPressDrag } from '../../../touch/useLongPressDrag';

/**
 * The K5 long-press drag of a board settings row (rule, column, lane) by its grip. While lifted it reports the
 * `data-drop-target` under the finger; it reports the drop, and the end of the drag, let go or cancelled.
 */
export function useRowTouchDrag({
  onOver,
  onDrop,
  onEnd,
}: {
  /** The lifted row is over the row with this id (null: off every row that takes a drop). */
  onOver(targetId: string | null): void;
  /** The grip was let go over the row with this id (null: off every row that takes a drop). */
  onDrop(targetId: string | null): void;
  /** The drag ended, let go or cancelled. */
  onEnd(): void;
}) {
  const drag = useLongPressDrag({
    handle: true,
    onMove: (target) => onOver(target?.dataset.dropTarget ?? null),
    onDrop: (target) => onDrop(target?.dataset.dropTarget ?? null),
  });
  // A touchcancel ends the drag with no drop, so the end is the lifted state going away, not onDrop.
  const wasLifted = useRef(false);
  const ended = useEffectEvent(onEnd);
  useEffect(() => {
    if (wasLifted.current && !drag.lifted) ended();
    wasLifted.current = drag.lifted;
  }, [drag.lifted]);
  return drag;
}
