import { featureBoardKeys } from '@shiplean/core/api/featureBoard';
import { useQueryClient } from '@tanstack/react-query';
import { createContext, useContext, useEffect, useMemo, useRef, type ReactNode } from 'react';
import { useActionRunner, usePending } from '../actions/ActionRunnerProvider';
import { UNDO } from '../actions/undo';
import type { DispatchOptions, Pending } from '../actions/types';
import type { FeaturesAction } from './actions';
import { featuresExecutor } from './executor';
import { useInvalidateFeatures } from './queries';

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
  useRefreshOnConflict();
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

type FeaturesPending = Pending & { action: FeaturesAction };

const isFeaturesPending = (p: Pending): p is FeaturesPending => p.action.area === 'features';

// A change refused because what it was sent against moved on since it was read.
const isConflict = (p: Pending) => p.state === 'failed' && p.failure === 'conflict';

// When a Features change is refused with a conflict, tap or voice, the overview and the Feature's board are read again,
// so the screen (and a voice preview waiting on it) holds the server's item at its new revision. Each refusal reads
// once; a batch of refusals in one project reads the overview once. A refusal already there when first seen (kept from
// before a restart) refreshes nothing: the queries load fresh then.
function useRefreshOnConflict() {
  const pending = usePending().filter(isFeaturesPending);
  const client = useQueryClient();
  const invalidateFeatures = useInvalidateFeatures();
  const seen = useRef<Set<string> | null>(null);
  useEffect(() => {
    const refused = pending.filter(isConflict).map((p) => ({ at: p.id, action: p.action }));
    if (seen.current === null) {
      seen.current = new Set(refused.map((r) => r.at));
      return;
    }
    const known = seen.current;
    const projects = new Set<string>();
    const boards = new Map<string, { projectId: string; key: string }>();
    for (const { at, action } of refused) {
      if (known.has(at)) continue;
      known.add(at);
      // Only the kinds sent at a revision or a rank carry their project; those are the ones the server refuses as stale.
      if (!('projectId' in action)) continue;
      projects.add(action.projectId);
      if (action.key !== null)
        boards.set(`${action.projectId}|${action.key}`, {
          projectId: action.projectId,
          key: action.key,
        });
    }
    for (const projectId of projects) void invalidateFeatures(projectId);
    for (const { projectId, key } of boards.values())
      void client.invalidateQueries({ queryKey: featureBoardKeys.board(projectId, key) });
    // Forget refusals no longer held, so a change refused, retried and refused again reads again.
    const now = new Set(refused.map((r) => r.at));
    for (const at of [...known]) if (!now.has(at)) known.delete(at);
  }, [pending, client, invalidateFeatures]);
}
