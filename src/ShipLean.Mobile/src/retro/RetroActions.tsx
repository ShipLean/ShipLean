import { createContext, useContext, useEffect, useMemo, type ReactNode } from 'react';
import { useActionRunner } from '../actions/ActionRunnerProvider';
import { UNDO } from '../actions/undo';
import type { DispatchOptions } from '../actions/types';
import type { RetroAction } from './actions';
import { retroExecutor } from './executor';

export interface RetroActions {
  // Returns the batch id. Defaults: the tap window, origin 'tap', all or nothing.
  dispatch(a: RetroAction, opts?: Partial<DispatchOptions>): string;
}

const RetroActionsContext = createContext<RetroActions | null>(null);

// Registers the Retro executor on the session's runner.
export function RetroActionsProvider({ children }: { children: ReactNode }) {
  const runner = useActionRunner();
  useEffect(() => {
    runner.register(retroExecutor);
  }, [runner]);
  const actions = useMemo<RetroActions>(
    () => ({
      dispatch: (a, opts) =>
        runner.dispatch([a], { undo: UNDO.tap, origin: 'tap', allOrNothing: true, ...opts }),
    }),
    [runner],
  );
  return <RetroActionsContext.Provider value={actions}>{children}</RetroActionsContext.Provider>;
}

export function useRetroActions(): RetroActions {
  const actions = useContext(RetroActionsContext);
  if (actions === null) throw new Error('useRetroActions needs a RetroActionsProvider above it.');
  return actions;
}
