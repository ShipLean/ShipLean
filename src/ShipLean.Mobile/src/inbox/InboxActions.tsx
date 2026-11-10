import { asksKeys } from '@shiplean/core/api/asks';
import { useQueryClient } from '@tanstack/react-query';
import { createContext, useContext, useEffect, useMemo, useRef, type ReactNode } from 'react';
import { useActionRunner, usePending } from '../actions/ActionRunnerProvider';
import type { AppAction, Pending } from '../actions/types';
import { UNDO } from '../actions/undo';
import type { InboxAction, PendingAnswer } from './actions';
import { inboxExecutor } from './executor';
import { useInvalidateInbox } from './queries';
import { foldAnswers, partsOf, toBatch, type InboxPending } from './reviewBatch';

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
const isInboxPending = (p: Pending): p is InboxPending => isInboxAction(p.action);

// Registers the Inbox executor on the session's runner and gives the Inbox its dispatch and pendings.
export function InboxActionsProvider({ children }: { children: ReactNode }) {
  const runner = useActionRunner();
  useEffect(() => {
    runner.register(inboxExecutor);
  }, [runner]);
  const all = usePending();
  const pending = useMemo(() => all.filter(isInboxPending), [all]);
  useInvalidateOnSent(pending);
  const actions = useMemo<InboxActions>(
    () => ({
      dispatch: (a) =>
        runner.dispatch(toBatch(a), { undo: UNDO.tap, origin: 'tap', allOrNothing: true }),
      undo: (batchId) => {
        void runner.undo(batchId);
      },
      // id is an answer's id or its batch id. A review's failed parts are retried together, in order.
      retry: (id) => {
        const inbox = runner.pending().filter(isInboxPending);
        const hit = inbox.filter((p) => p.id === id);
        const targets = hit.length > 0 ? hit : inbox.filter((p) => p.batchId === id);
        const parts = new Set(targets.flatMap((p) => partsOf(p, inbox)));
        for (const p of inbox)
          if (parts.has(p) && (p.state === 'failed' || p.state === 'uncertain')) runner.retry(p.id);
      },
      pending: foldAnswers(pending),
    }),
    [runner, pending],
  );
  return <InboxActionsContext.Provider value={actions}>{children}</InboxActionsContext.Provider>;
}

// When every part of a batch has been sent, the project's Inbox and Answered are refreshed once. A batch already sent
// when first seen (kept from before a restart) refreshes nothing: the queries load fresh then. Each sent part's work item
// reads its asks again too, so a Work item's Needs you is not left stale by an answer from the Inbox.
function useInvalidateOnSent(pending: InboxPending[]) {
  const invalidate = useInvalidateInbox();
  const client = useQueryClient();
  const seen = useRef(new Map<string, boolean>());
  useEffect(() => {
    const batches = new Map<string, InboxPending[]>();
    for (const p of pending) batches.set(p.batchId, [...(batches.get(p.batchId) ?? []), p]);
    for (const [batchId, parts] of batches) {
      const sent = parts.every((p) => p.state === 'sent');
      const before = seen.current.get(batchId);
      seen.current.set(batchId, sent);
      if (sent && before === false) {
        for (const projectId of new Set(parts.map((p) => p.action.projectId)))
          void invalidate(projectId);
        for (const p of parts)
          void client.invalidateQueries({
            queryKey: asksKeys.item(p.action.projectId, p.action.key),
          });
      }
    }
    // Forget batches the runner no longer holds.
    for (const batchId of seen.current.keys())
      if (!batches.has(batchId)) seen.current.delete(batchId);
  }, [pending, invalidate, client]);
}

export function useInboxActions(): InboxActions {
  const actions = useContext(InboxActionsContext);
  if (actions === null) throw new Error('useInboxActions needs an InboxActionsProvider above it.');
  return actions;
}
