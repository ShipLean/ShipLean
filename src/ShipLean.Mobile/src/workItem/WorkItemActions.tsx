import { asksKeys } from '@shiplean/core/api/asks';
import { commentKeys } from '@shiplean/core/api/commentsApi';
import { useQueryClient } from '@tanstack/react-query';
import { createContext, useContext, useEffect, useMemo, useRef, type ReactNode } from 'react';
import { useActionRunner, usePending } from '../actions/ActionRunnerProvider';
import { UNDO } from '../actions/undo';
import type { DispatchOptions, Pending } from '../actions/types';
import type { WorkItemAction } from './actions';
import { workItemExecutor } from './executor';
import { useInvalidateWorkItem } from './queries';

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
  useRefreshOnSettled();
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

type WorkItemPending = Pending & { action: WorkItemAction };

const isWorkItemPending = (p: Pending): p is WorkItemPending => p.action.area === 'workItem';

// A change has settled when the server took it, refused it, or a sent one was undone. Each of those moves what the
// server holds (or shows the change never landed), so each is a point to read again.
const settledAs = (p: Pending): string | null =>
  p.state === 'sent' || p.state === 'failed' ? `${p.id}:${p.state}:${p.undoOutcome ?? ''}` : null;

const ASK_KINDS: ReadonlySet<WorkItemAction['kind']> = new Set(['approve', 'discuss', 'reply']);

// When a Work item change is sent, refused or undone, what it touched is read again, wherever it was dispatched from: a
// note its item's comments; any other change the item and everything read about it (useInvalidateWorkItem), and an
// answer to a decision or question the item's asks too. A change already settled when first seen (kept from before a
// restart) refreshes nothing: the queries load fresh then.
function useRefreshOnSettled() {
  const pending = usePending().filter(isWorkItemPending);
  const client = useQueryClient();
  const invalidateItem = useInvalidateWorkItem();
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
    // What this settle touched, each once: a batch of changes to one item is one read, not one per change.
    const comments = new Set<string>();
    const items = new Map<string, { projectId: string; key: string; itemId: string }>();
    const asks = new Map<string, { projectId: string; key: string }>();
    for (const { at, id, action } of settled) {
      if (known.has(at)) continue;
      known.set(at, id);
      if (action.kind === 'addNote') {
        comments.add(action.itemId);
        continue;
      }
      const { projectId, key, itemId } = action;
      items.set(`${projectId}|${key}|${itemId}`, { projectId, key, itemId });
      if (ASK_KINDS.has(action.kind)) asks.set(`${projectId}|${key}`, { projectId, key });
    }
    for (const itemId of comments)
      void client.invalidateQueries({ queryKey: commentKeys.list(itemId) });
    for (const { projectId, key, itemId } of items.values())
      void invalidateItem(projectId, key, itemId);
    for (const { projectId, key } of asks.values())
      void client.invalidateQueries({ queryKey: asksKeys.item(projectId, key) });
    // Forget what is no longer settled, so a change refused, retried and refused again is a fresh settlement.
    const now = new Set(settled.map((s) => s.at));
    for (const at of [...known.keys()]) if (!now.has(at)) known.delete(at);
  }, [pending, client, invalidateItem]);
}
