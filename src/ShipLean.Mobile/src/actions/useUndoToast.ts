import { useEffect, useState } from 'react';
import { useActionRunner, useRunnerChanges } from './ActionRunnerProvider';
import { systemNow } from './clock';
import type { Pending, UndoOutcome } from './types';

export interface UndoToast {
  batchId: string;
  // Each action's describe(a).text, joined " · ".
  text: string;
  undo(): Promise<UndoOutcome[]>;
}

function newestOpenBatch(pending: Pending[], now: number): Pending[] {
  const open = pending.filter((p) => p.undoUntil !== null && Date.parse(p.undoUntil) > now);
  if (open.length === 0) return [];
  // Newest by createdAt; on a tie the later dispatch, which comes later in pending().
  const newest = open.reduce((a, b) =>
    Date.parse(b.createdAt) >= Date.parse(a.createdAt) ? b : a,
  );
  return open.filter((p) => p.batchId === newest.batchId);
}

// The newest batch whose undo window is still open, hold or after, or null.
export function useUndoToast(now: () => Date = systemNow): UndoToast | null {
  const runner = useActionRunner();
  useRunnerChanges(runner);
  const [, setTick] = useState(0);
  const at = now().getTime();
  const batch = newestOpenBatch(runner.pending(), at);
  const closesAt = batch.length === 0 ? null : Date.parse(batch[0].undoUntil as string);

  // Render again when the window closes, so the toast goes.
  useEffect(() => {
    if (closesAt === null) return;
    const timer = setTimeout(() => setTick((t) => t + 1), Math.max(0, closesAt - at));
    return () => clearTimeout(timer);
  }, [closesAt, at]);

  if (batch.length === 0) return null;
  const batchId = batch[0].batchId;
  return {
    batchId,
    text: batch.map((p) => runner.describe(p.action).text).join(' · '),
    undo: () => runner.undo(batchId),
  };
}
