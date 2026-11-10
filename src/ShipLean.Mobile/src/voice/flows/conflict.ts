// A 409 on an applied voice batch: nothing was applied, so the preview is rebuilt and asked again.
import type { Pending, PreviewLine } from '../../actions/types';
import type { VoiceInternals } from '../internals';
import { pairs } from '../machine';
import { sameAction, sameLine, showRebuilt } from './common';

export const CHANGED_HERE = 'Someone else changed this';

// Only a batch voice or typing sent from the last preview: the runner reports every 409, the Inbox's taps too. The
// same calls are resolved again against the screen as it is now (the spec's current context()), the rows that read
// differently and the row that was refused are marked, and the lines the person removed stay removed. Nothing is sent
// until the person applies again.
export function onConflict(v: VoiceInternals, batchId: string, lines: PreviewLine[]): void {
  const last = v.last();
  if (last === null || !last.result.ok) return;
  const parts = v.runner.pending().filter((p) => p.batchId === batchId);
  if (parts.length === 0 || parts.some((p) => p.origin === 'tap')) return;
  const { actions } = last.result;
  if (!parts.every((p) => actions.some((a) => sameAction(a, p.action)))) return;

  const before = pairs(last.result);
  const sentIds = new Set(
    before.filter((b) => parts.some((p) => sameAction(b.action, p.action))).map((b) => b.line.id),
  );
  const refused = parts.find((p) => p.failure === 'conflict');
  const source = parts[0].origin === 'typed' ? 'typed' : 'voice';

  const result = v.resolveCalls(last.calls, last.transcript);
  const shown = showRebuilt(v, result, last.transcript, source);
  if (shown.kind === 'view') {
    v.set({
      kind: 'failure',
      failure: { kind: 'noValidAction', detail: { action: null, reason: 'none' } },
      heard: last.transcript.text,
      source,
    });
    return;
  }
  if (shown.kind !== 'preview' || !result.ok) return;
  const now = pairs(result);
  const marks = now
    .filter(({ line, action }) => {
      const old = before.find((b) => b.line.id === line.id)?.line;
      return (old !== undefined && !sameLine(old, line)) || isRefused(refused, action);
    })
    .map(({ line }) => ({ lineId: line.id, note: CHANGED_HERE }));
  const removed = now
    .filter(({ line }) => before.some((b) => b.line.id === line.id) && !sentIds.has(line.id))
    .map(({ line }) => line.id);
  // lines are the batch as the runner describes it; the rows are compared with the preview the person saw instead.
  void lines;
  v.set({ ...shown.state, marks, removed });
}

// The rebuilt action for the refused one: the same kind on the same item (its revision and from value are new).
function isRefused(refused: Pending | undefined, a: Pending['action']): boolean {
  if (refused === undefined) return false;
  const r = refused.action;
  return r.area === a.area && r.kind === a.kind && r.itemId === a.itemId && r.key === a.key;
}
