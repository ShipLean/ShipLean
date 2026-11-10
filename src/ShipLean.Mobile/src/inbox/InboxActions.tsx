import { createContext, useContext, useEffect, useMemo, type ReactNode } from 'react';
import { useActionRunner, usePending } from '../actions/ActionRunnerProvider';
import type { AppAction, Pending } from '../actions/types';
import { UNDO } from '../actions/undo';
import { labelsOf, type InboxAction, type PendingAnswer } from './actions';
import { inboxExecutor } from './executor';

export interface InboxActions {
  // A tap: held for the tap window (5 s, P4), then sent. Returns the batch id.
  dispatch(a: InboxAction): string;
  undo(batchId: string): void;
  retry(id: string): void;
  // The runner's Inbox pendings, with the labels each answers.
  pending: PendingAnswer[];
}

export const InboxActionsContext = createContext<InboxActions | null>(null);

const isInboxAction = (a: AppAction): a is InboxAction => a.area === 'inbox';
const isInboxPending = (p: Pending): p is Pending & { action: InboxAction } =>
  isInboxAction(p.action);

// Registers the Inbox executor on the session's runner and gives the Inbox its dispatch and pendings.
export function InboxActionsProvider({ children }: { children: ReactNode }) {
  const runner = useActionRunner();
  useEffect(() => {
    runner.register(inboxExecutor);
  }, [runner]);
  const pending = usePending();
  const actions = useMemo<InboxActions>(
    () => ({
      dispatch: (a) => runner.dispatch([a], { undo: UNDO.tap, origin: 'tap', allOrNothing: true }),
      undo: (batchId) => {
        void runner.undo(batchId);
      },
      retry: (id) => runner.retry(id),
      pending: pending.filter(isInboxPending).map((p) => ({ ...p, labels: labelsOf(p.action) })),
    }),
    [runner, pending],
  );
  return <InboxActionsContext.Provider value={actions}>{children}</InboxActionsContext.Provider>;
}

export function useInboxActions(): InboxActions {
  const actions = useContext(InboxActionsContext);
  if (actions === null) throw new Error('useInboxActions needs an InboxActionsProvider above it.');
  return actions;
}
