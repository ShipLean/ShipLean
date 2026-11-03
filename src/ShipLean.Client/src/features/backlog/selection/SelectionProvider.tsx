import { useCallback, useMemo, useReducer, useState, type ReactNode } from 'react';
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
  const [shownIds, shown] = useState<readonly string[]>([]);
  // The owner may pass a new array each render; the same ids keep the same state.
  const setShownIds = useCallback(
    (ids: readonly string[]) =>
      shown((current) =>
        current.length === ids.length && current.every((id, i) => id === ids[i]) ? current : ids,
      ),
    [],
  );
  const value = useMemo(
    () => ({ state, dispatch, focusedId, setFocused, walkOrder, shownIds, setShownIds }),
    [state, focusedId, walkOrder, shownIds, setShownIds],
  );
  return <SelectionContext.Provider value={value}>{children}</SelectionContext.Provider>;
}
