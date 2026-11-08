import { createContext, createElement, useContext, useMemo, useState, type ReactNode } from 'react';

export interface TouchSelectMode {
  /** True while the backlog is in Select mode at touch: rows show a checkbox in the handle slot and a tap selects. */
  selecting: boolean;
  start: () => void;
  stop: () => void;
}

const TouchSelectModeContext = createContext<TouchSelectMode | null>(null);

const noop = () => undefined;
const off: TouchSelectMode = { selecting: false, start: noop, stop: noop };

/** Holds the touch Select mode for the backlog slots (BacklogSlots mounts it), so every mode's rows and bars read one flag. */
export function TouchSelectModeProvider({ children }: { children: ReactNode }) {
  const [selecting, setSelecting] = useState(false);
  const value = useMemo<TouchSelectMode>(
    () => ({ selecting, start: () => setSelecting(true), stop: () => setSelecting(false) }),
    [selecting],
  );
  return createElement(TouchSelectModeContext.Provider, { value }, children);
}

/** The touch Select mode. Outside a TouchSelectModeProvider it is off, and start and stop do nothing. */
export function useTouchSelectMode(): TouchSelectMode {
  return useContext(TouchSelectModeContext) ?? off;
}
