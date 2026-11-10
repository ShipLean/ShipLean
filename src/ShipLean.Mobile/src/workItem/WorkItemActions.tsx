import { createContext, useContext, useEffect, useMemo, type ReactNode } from 'react';
import { useActionRunner } from '../actions/ActionRunnerProvider';
import { UNDO } from '../actions/undo';
import type { DispatchOptions } from '../actions/types';
import type { WorkItemAction } from './actions';
import { workItemExecutor } from './executor';

export interface WorkItemActions {
  // Returns the batch id. A tap by default: held 5 s, all or nothing.
  dispatch(a: WorkItemAction, opts?: Partial<DispatchOptions>): string;
}

const WorkItemActionsContext = createContext<WorkItemActions | null>(null);

// Registers the Work item executor with the session's runner and gives the screen its dispatch.
export function WorkItemActionsProvider({ children }: { children: ReactNode }) {
  const runner = useActionRunner();
  useEffect(() => {
    runner.register(workItemExecutor);
  }, [runner]);
  const actions = useMemo<WorkItemActions>(
    () => ({
      dispatch: (a, opts) =>
        runner.dispatch([a], { undo: UNDO.tap, origin: 'tap', allOrNothing: true, ...opts }),
    }),
    [runner],
  );
  return (
    <WorkItemActionsContext.Provider value={actions}>{children}</WorkItemActionsContext.Provider>
  );
}

export function useWorkItemActions(): WorkItemActions {
  const actions = useContext(WorkItemActionsContext);
  if (actions === null)
    throw new Error('useWorkItemActions needs a WorkItemActionsProvider above it.');
  return actions;
}
