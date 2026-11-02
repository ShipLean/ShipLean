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
  const click = (event: MouseEvent<HTMLTableRowElement>) => {
    selection.setFocused(row.id);
    if (event.target instanceof Element && event.target.closest(inside) !== null) return;
    if (event.shiftKey) selection.rangeTo(row.id);
    else if (event.ctrlKey || event.metaKey) selection.toggle(row.id);
  };
  return {
    tabIndex: 0,
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
