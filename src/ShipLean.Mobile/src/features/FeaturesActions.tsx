import { createContext, useContext, useEffect, useMemo, type ReactNode } from 'react';
import { useActionRunner } from '../actions/ActionRunnerProvider';
import { UNDO } from '../actions/undo';
import type { DispatchOptions } from '../actions/types';
import type { FeaturesAction } from './actions';
import { featuresExecutor } from './executor';

export interface FeaturesActions {
  // Returns the batch id. A tap by default: held 5 s, all or nothing.
  dispatch(a: FeaturesAction, opts?: Partial<DispatchOptions>): string;
}

const FeaturesActionsContext = createContext<FeaturesActions | null>(null);

// Registers the Features executor with the session's runner and gives the area its dispatch.
export function FeaturesActionsProvider({ children }: { children: ReactNode }) {
  const runner = useActionRunner();
  useEffect(() => {
    runner.register(featuresExecutor);
  }, [runner]);
  const actions = useMemo<FeaturesActions>(
    () => ({
      dispatch: (a, opts) =>
        runner.dispatch([a], { undo: UNDO.tap, origin: 'tap', allOrNothing: true, ...opts }),
    }),
    [runner],
  );
  return (
    <FeaturesActionsContext.Provider value={actions}>{children}</FeaturesActionsContext.Provider>
  );
}

export function useFeaturesActions(): FeaturesActions {
  const actions = useContext(FeaturesActionsContext);
  if (actions === null)
    throw new Error('useFeaturesActions needs a FeaturesActionsProvider above it.');
  return actions;
}
