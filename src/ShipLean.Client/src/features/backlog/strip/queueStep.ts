/** The item the not-estimated queue is on: its id, and its place among the queue's items. */
export interface QueueStep {
  id: string;
  position: number;
  total: number;
}

/**
 * The next item in the queue: the first of `queuedIds` (in order) that is still flagged and not yet passed (picked or
 * skipped while the queue is open). Null when none is left. Position counts from 1 over all of `queuedIds`.
 */
export function queueStep(
  queuedIds: readonly string[],
  flagged: ReadonlySet<string>,
  passed: ReadonlySet<string>,
): QueueStep | null {
  const at = queuedIds.findIndex((id) => flagged.has(id) && !passed.has(id));
  return at < 0 ? null : { id: queuedIds[at], position: at + 1, total: queuedIds.length };
}
