import type { HTMLAttributes } from 'react';
import type { BacklogRowDto } from '../../../api/views';

/** What a row's `<tr>` takes from the gestures: handlers and `data-*` attributes. */
export type RowGestureProps = HTMLAttributes<HTMLTableRowElement> &
  Record<`data-${string}`, string | boolean | undefined>;

const none: RowGestureProps = {};

/** The click, focus and key gestures of one row, to spread on its `<tr>`. */
// TODO: focus, Ctrl/Shift-click and the selected state replace this empty set.
export function useRowGestures(row: BacklogRowDto): RowGestureProps {
  void row;
  return none;
}
