import { useState } from 'react';

/** Where the drop line goes on the row under a lifted row: the side the dragged row will land on. */
export type DropLine = 'above' | 'below';

/** What a row needs to draw the drop line and report its own touch drag (useRowTouchDrag). */
export interface DropLineProps {
  dropLine: DropLine | undefined;
  onTouchOver(targetId: string | null): void;
  onTouchDragEnd(): void;
}

/**
 * The drop line of a pane's touch reorder (design settings-board-styles-drag, SHIP-1809 P4): while a row is lifted,
 * the row under the finger draws a signal line on the side the dragged row will land. `ids` is the rows' order.
 */
export function useDropLine(ids: readonly string[]) {
  const [dragging, setDragging] = useState<{ id: string; over: string | null }>();
  return (id: string): DropLineProps => ({
    dropLine:
      dragging === undefined || dragging.over !== id || dragging.id === id
        ? undefined
        : ids.indexOf(id) > ids.indexOf(dragging.id)
          ? 'below'
          : 'above',
    onTouchOver: (over) => setDragging({ id, over }),
    onTouchDragEnd: () => setDragging((d) => (d?.id === id ? undefined : d)),
  });
}

/** The line as an inset signal shadow (touch only), as the States rows draw theirs. */
export function dropLineClass(line: DropLine | undefined) {
  if (line === 'above') return 'touch:shadow-[inset_0_2px_0_var(--color-signal)]';
  if (line === 'below') return 'touch:shadow-[inset_0_-2px_0_var(--color-signal)]';
  return '';
}

/** The same line for a table row: drawn on its cells, which carry a box shadow where a collapsed-border row may not. */
export function dropLineCellsClass(line: DropLine | undefined) {
  if (line === 'above') return 'touch:*:shadow-[inset_0_2px_0_var(--color-signal)]';
  if (line === 'below') return 'touch:*:shadow-[inset_0_-2px_0_var(--color-signal)]';
  return '';
}
