import { createContext, useContext, useEffect, useMemo } from 'react';
import type { Dispatch } from 'react';
import type { BacklogRowDto } from '@shiplean/core/api/views';
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
  /** Every shown row's id in screen order, as the screen's owner last gave it; what range and select-all run over. */
  shownIds: readonly string[];
  setShownIds: (ids: readonly string[]) => void;
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
  /** Adds the rows to the selection, keeping the rest. */
  addMany: (ids: readonly string[]) => void;
  removeMany: (ids: readonly string[]) => void;
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
  addMany: noop,
  removeMany: noop,
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
  const setShownIds = context?.setShownIds;
  useEffect(() => {
    if (shownIds === undefined) return;
    setShownIds?.(shownIds);
    dispatch?.({ type: 'prune', order: shownIds });
  }, [dispatch, setShownIds, shownIds]);
  return useMemo(() => {
    if (context === null) return inert;
    const { state, focusedId, setFocused, walkOrder, shownIds: owned } = context;
    return {
      ids: state.ids,
      has: (id) => state.ids.has(id),
      count: state.ids.size,
      orderedIds: walkOrder ?? owned,
      toggle: (id) => context.dispatch({ type: 'toggle', id }),
      // A range walks the order the rows are shown in, so a collapsed section's rows are skipped.
      rangeTo: (id) => context.dispatch({ type: 'range', id, order: walkOrder ?? owned }),
      addMany: (ids) => context.dispatch({ type: 'addMany', ids }),
      removeMany: (ids) => context.dispatch({ type: 'removeMany', ids }),
      selectAll: () => context.dispatch({ type: 'selectAll', order: owned }),
      clear: () => context.dispatch({ type: 'clear' }),
      focusedId,
      setFocused,
      selectedRows: (rows) => pickRows(state, rows),
    };
  }, [context]);
}
