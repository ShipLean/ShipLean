import { useMemo, useReducer, useState, type ReactNode } from 'react';
import { emptySelection, selectionReducer } from './selectionModel';
import { SelectionContext } from './useSelection';

/** One selection and focus for a backlog screen. `walkOrder` is the ids J/K and Enter walk, collapsed sections left out. */
export function SelectionProvider({
  walkOrder,
  children,
}: {
  walkOrder?: readonly string[];
  children: ReactNode;
}) {
  const [state, dispatch] = useReducer(selectionReducer, emptySelection);
  const [focusedId, setFocused] = useState<string | null>(null);
  const value = useMemo(
    () => ({ state, dispatch, focusedId, setFocused, walkOrder }),
    [state, focusedId, walkOrder],
  );
  return <SelectionContext.Provider value={value}>{children}</SelectionContext.Provider>;
}
