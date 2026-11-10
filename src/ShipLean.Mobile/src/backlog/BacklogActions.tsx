import { createContext, useContext, useEffect, useMemo, useRef, type ReactNode } from 'react';
import { useActionRunner, usePending } from '../actions/ActionRunnerProvider';
import { UNDO } from '../actions/undo';
import type { DispatchOptions, Pending } from '../actions/types';
import type { BacklogAction } from './actions';
import { backlogExecutor } from './executor';
import { useInvalidateBacklog } from './queries';

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
  useRefreshOnSettled();
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

type BacklogPending = Pending & { action: BacklogAction };

const isBacklogPending = (p: Pending): p is BacklogPending => p.action.area === 'backlog';

// A change has settled when the server took it, refused it, or a sent one was undone. Each of those moves what the
// server holds (or shows the change never landed), so each is a point to read again.
const settledAs = (p: Pending): string | null =>
  p.state === 'sent' || p.state === 'failed' ? `${p.id}:${p.state}:${p.undoOutcome ?? ''}` : null;

// When a move or an add is sent, refused or undone, the project's backlog is read again, wherever it was dispatched from
// (a tap or a voice command). A change already settled when first seen (kept from before a restart) refreshes nothing:
// the list loads fresh then.
function useRefreshOnSettled() {
  const pending = usePending().filter(isBacklogPending);
  const invalidate = useInvalidateBacklog();
  // Each settlement already handled, to the id of its Pending.
  const seen = useRef<Map<string, string> | null>(null);
  useEffect(() => {
    const settled = pending.flatMap((p) => {
      const at = settledAs(p);
      return at === null ? [] : [{ at, id: p.id, action: p.action }];
    });
    if (seen.current === null) {
      seen.current = new Map(settled.map((s) => [s.at, s.id]));
      return;
    }
    const known = seen.current;
    for (const { at, id, action } of settled) {
      if (known.has(at)) continue;
      known.set(at, id);
      if (action.kind === 'move' || action.kind === 'addItem') void invalidate(action.projectId);
    }
    // Forget changes the runner no longer holds.
    const live = new Set(pending.map((p) => p.id));
    for (const [at, id] of known) if (!live.has(id)) known.delete(at);
  }, [pending, invalidate]);
}
