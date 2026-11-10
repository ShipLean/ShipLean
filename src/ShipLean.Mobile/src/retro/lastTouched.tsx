// D8: "that" means the point the person last touched (tapped or +1'd). RT1 sets it, RT-V reads it.
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';

export interface LastTouchedPoint {
  pointId: string | null;
  touch(pointId: string): void;
  clear(): void;
}

interface Selection extends LastTouchedPoint {
  showSprint(sprintId: string | null): void;
}

const SelectionContext = createContext<Selection | null>(null);

export function RetroSelectionProvider({ children }: { children: ReactNode }) {
  const [pointId, setPointId] = useState<string | null>(null);
  const shown = useRef<string | null | undefined>(undefined);
  const touch = useCallback((id: string) => setPointId(id), []);
  const clear = useCallback(() => setPointId(null), []);
  const showSprint = useCallback((sprintId: string | null) => {
    if (shown.current === sprintId) return;
    shown.current = sprintId;
    setPointId(null);
  }, []);
  const value = useMemo(
    () => ({ pointId, touch, clear, showSprint }),
    [pointId, touch, clear, showSprint],
  );
  return <SelectionContext.Provider value={value}>{children}</SelectionContext.Provider>;
}

function useSelection(): Selection {
  const selection = useContext(SelectionContext);
  if (selection === null) throw new Error('This needs a RetroSelectionProvider above it.');
  return selection;
}

export function useLastTouchedPoint(): LastTouchedPoint {
  const { pointId, touch, clear } = useSelection();
  return { pointId, touch, clear };
}

// The Retro screen calls it with the sprint it shows; the last touched point is forgotten when that changes.
export function useShownRetroSprint(sprintId: string | null): void {
  const { showSprint } = useSelection();
  useEffect(() => showSprint(sprintId), [showSprint, sprintId]);
}
