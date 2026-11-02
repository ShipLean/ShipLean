import { createContext, useContext, useEffect, useMemo } from 'react';
import type { Dispatch } from 'react';
import type { BacklogRowDto } from '../../../api/views';
import {
  emptySelection,
  selectedRows as pickRows,
  type SelectionAction,
  type SelectionState,
} from './selectionModel';

export interface SelectionContextValue {
  state: SelectionState;
  dispatch: Dispatch<SelectionAction>;
  focusedId: string | null;
  setFocused: (id: string | null) => void;
  /** The ids a walk (J/K, Enter) visits, collapsed sections left out; absent = every shown row. */
  walkOrder: readonly string[] | undefined;
}

export const SelectionContext = createContext<SelectionContextValue | null>(null);

export interface Selection {
  ids: ReadonlySet<string>;
  has: (id: string) => boolean;
  count: number;
  /** Shown rows to walk down, collapsed sections left out. */
  orderedIds: readonly string[];
  toggle: (id: string) => void;
  rangeTo: (id: string) => void;
  selectAll: () => void;
  clear: () => void;
  focusedId: string | null;
  setFocused: (id: string | null) => void;
  /** The selected rows in `rows` order (rank order), never click order. */
  selectedRows: (rows: readonly BacklogRowDto[]) => BacklogRowDto[];
}

const noop = () => undefined;
const emptyIds: readonly string[] = [];
const inert: Selection = {
  ids: emptySelection.ids,
  has: () => false,
  count: 0,
  orderedIds: emptyIds,
  toggle: noop,
  rangeTo: noop,
  selectAll: noop,
  clear: noop,
  focusedId: null,
  setFocused: noop,
  selectedRows: () => [],
};

/**
 * The screen's selection. The screen's owner passes `shownIds`, every shown row's id in screen order, and rows that
 * leave it leave the selection; a cell or bar reading the selection passes nothing. Outside a SelectionProvider (Tree mode) it is inert: empty, and every call does nothing.
 */
export function useSelection(shownIds?: readonly string[]): Selection {
  const context = useContext(SelectionContext);
  const dispatch = context?.dispatch;
  useEffect(() => {
    if (shownIds !== undefined) dispatch?.({ type: 'prune', order: shownIds ?? emptyIds });
  }, [dispatch, shownIds]);
  return useMemo(() => {
    if (context === null) return inert;
    const { state, focusedId, setFocused, walkOrder } = context;
    return {
      ids: state.ids,
      has: (id) => state.ids.has(id),
      count: state.ids.size,
      orderedIds: walkOrder ?? shownIds ?? emptyIds,
      toggle: (id) => context.dispatch({ type: 'toggle', id }),
      rangeTo: (id) => context.dispatch({ type: 'range', id, order: shownIds ?? emptyIds }),
      selectAll: () => context.dispatch({ type: 'selectAll', order: shownIds ?? emptyIds }),
      clear: () => context.dispatch({ type: 'clear' }),
      focusedId,
      setFocused,
      selectedRows: (rows) => pickRows(state, rows),
    };
  }, [context, shownIds]);
}
