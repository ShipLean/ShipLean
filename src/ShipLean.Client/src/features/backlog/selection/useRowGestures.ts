import type { MouseEvent } from 'react';
import type { HTMLAttributes } from 'react';
import type { BacklogRowDto } from '../../../api/views';
import { useSelection } from './useSelection';

/** What a row's `<tr>` takes from the gestures: handlers and `data-*` attributes. */
export type RowGestureProps = HTMLAttributes<HTMLTableRowElement> &
  Record<`data-${string}`, string | boolean | undefined>;

const inside = 'a, button, input, select, textarea, label';

/**
 * The click and focus gestures of one row, to spread on its `<tr>`. A plain click only moves focus, so a stray click
 * can't lose a selection; Ctrl/Cmd toggles the row and Shift selects a range from the anchor. Controls inside the row
 * (the checkbox, the title link, cells that edit) keep their own clicks.
 */
export function useRowGestures(row: BacklogRowDto): RowGestureProps {
  const selection = useSelection();
  const selected = selection.has(row.id);
  // Roving tab stop: the focused row is the one row in the tab order, or the first when none is focused (or the focused
  // one is no longer shown). Without a shown order every row stays a tab stop.
  const { focusedId, orderedIds } = selection;
  const stop =
    focusedId !== null && orderedIds.includes(focusedId) ? focusedId : (orderedIds[0] ?? row.id);
  const click = (event: MouseEvent<HTMLTableRowElement>) => {
    selection.setFocused(row.id);
    if (event.target instanceof Element && event.target.closest(inside) !== null) return;
    if (event.shiftKey) selection.rangeTo(row.id);
    else if (event.ctrlKey || event.metaKey) selection.toggle(row.id);
  };
  return {
    tabIndex: stop === row.id ? 0 : -1,
    'aria-keyshortcuts': 'J K X',
    'data-row-id': row.id,
    onClick: click,
    // Shift-click would otherwise select the text between the rows.
    onMouseDown: (event) => {
      if (event.shiftKey) event.preventDefault();
    },
    onFocus: (event) => {
      if (event.target === event.currentTarget) selection.setFocused(row.id);
    },
    'aria-selected': selected,
    'data-selected': selected,
    'data-focused': selection.focusedId === row.id,
  };
}
