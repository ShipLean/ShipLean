import { useEffect, type ReactNode } from 'react';
import { TouchDragGhost } from '../../../touch/TouchDragGhost';
import { useLongPressDrag } from '../../../touch/useLongPressDrag';
import { backlogTestIds as ids } from '../testIds';

/**
 * The drag handle of a backlog row at touch (900px and below, SHIP-716 D2): the rank on top, ⠿ under it, as tall as
 * the row and at least --touch-target wide. TouchRow's left-edge slot (`handle`) takes it; List, Planning and Tree
 * pass it through TouchRowList's `renderHandle`.
 *
 * Props, the surface wave 2 builds on:
 * - `rowId`: the row's id, for the handle's label.
 * - `rank`: the rank shown on top.
 * - `disabled?`: no drag starts (a filter, search or Select mode is on); the handle stays, and the page scrolls
 *   normally under the finger.
 * - `onDrop(targetId)`: the finger let go after a lift and a move. `targetId` is the `data-drop-target` of the element
 *   under it (TouchRow sets it to the row's id), or null off any target. Pass it to `useTouchReorder().drop`.
 * - `ghost?`: what follows the finger while lifted (a copy of the row); left out, a chip with the rank.
 * - `onLiftedChange?(lifted)`: called when the lift starts and ends, so the row can fade where it was.
 *
 * Only the handle starts a drag (K6's handle variant: touch-action none on it alone, a 450ms hold lifts). A swipe that
 * starts anywhere else scrolls the page, and one that starts on the handle before the hold scrolls it too.
 */
export function TouchHandle({
  rowId,
  rank,
  disabled = false,
  onDrop,
  ghost,
  onLiftedChange,
}: {
  rowId: string;
  rank: number | string;
  disabled?: boolean;
  onDrop: (targetId: string | null) => void;
  ghost?: ReactNode;
  onLiftedChange?: (lifted: boolean) => void;
}) {
  const drag = useLongPressDrag({
    handle: true,
    onDrop: (target) => onDrop(target?.dataset.dropTarget ?? null),
  });
  useEffect(() => onLiftedChange?.(drag.lifted), [drag.lifted, onLiftedChange]);
  const face = (
    <>
      <span data-testid={ids.rank} className="text-[length:var(--text-xs)] tabular-nums">
        {rank}
      </span>
      <span aria-hidden="true" className="leading-none">
        ⠿
      </span>
    </>
  );
  return (
    <>
      <button
        type="button"
        {...(disabled ? {} : drag.props)}
        data-testid={ids.touchHandle}
        data-row-id={rowId}
        aria-label={`Move ${rowId}`}
        disabled={disabled}
        className={`flex size-full min-h-[var(--touch-target)] min-w-[var(--touch-target)] flex-col items-center justify-center gap-0.5 text-ink-muted ${disabled ? 'opacity-40' : ''}`}
      >
        {face}
      </button>
      {drag.lifted ? (
        <TouchDragGhost x={drag.x} y={drag.y}>
          {ghost ?? (
            <div
              data-testid={ids.touchHandleGrip}
              className="flex min-h-14 min-w-[var(--touch-target)] flex-col items-center justify-center px-3"
            >
              {face}
            </div>
          )}
        </TouchDragGhost>
      ) : null}
    </>
  );
}
