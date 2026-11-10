import { createContext, useContext, useEffect, useMemo, type ReactNode } from 'react';
import { useActionRunner } from '../actions/ActionRunnerProvider';
import { UNDO } from '../actions/undo';
import type { DispatchOptions } from '../actions/types';
import type { BacklogAction } from './actions';
import { backlogExecutor } from './executor';

export interface BacklogActions {
  // Returns the batch id. A tap by default: held 5 s, all or nothing.
  dispatch(a: BacklogAction, opts?: Partial<DispatchOptions>): string;
}

const BacklogActionsContext = createContext<BacklogActions | null>(null);

// Registers the Backlog's executor with the session's runner and gives the area its dispatch.
export function BacklogActionsProvider({ children }: { children: ReactNode }) {
  const runner = useActionRunner();
  useEffect(() => {
    runner.register(backlogExecutor);
  }, [runner]);
  const actions = useMemo<BacklogActions>(
    () => ({
      dispatch: (a, opts) =>
        runner.dispatch([a], { undo: UNDO.tap, origin: 'tap', allOrNothing: true, ...opts }),
    }),
    [runner],
  );
  return (
    <BacklogActionsContext.Provider value={actions}>{children}</BacklogActionsContext.Provider>
  );
}

export function useBacklogActions(): BacklogActions {
  const actions = useContext(BacklogActionsContext);
  if (actions === null)
    throw new Error('useBacklogActions needs a BacklogActionsProvider above it.');
  return actions;
}
