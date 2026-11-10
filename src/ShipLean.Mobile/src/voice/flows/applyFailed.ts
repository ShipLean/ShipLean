// A voice batch that failed online: say what stayed applied, what was reversed and what wasn't sent.
import type { Pending } from '../../actions/types';
import type { ResolveResult } from '../contracts';
import type { VoiceInternals, VoiceUiStateOf } from '../internals';
import { rest, sameAction } from './common';

// The runner has already handled the batch (N8, A.2): it stopped at the first refusal and reversed, newest first, the
// sent actions that have a reverse. Each part is in one group.
export type ApplyGroup = 'stillApplied' | 'reversed' | 'notSent';

export function applyGroup(p: Pending): ApplyGroup {
  if (p.undoOutcome === 'undone') return 'reversed';
  if (p.state === 'sent') return 'stillApplied';
  return 'notSent';
}

// The batch's lines, one per part in the runner's order, worded as the preview showed them, with the reason the
// runner gave for the part that was refused (or got no answer).
export function onApplyFailed(v: VoiceInternals, batchId: string, pendings: Pending[]): void {
  const last = v.last();
  const shown = last !== null && last.result.ok ? lineOf(last.result) : () => undefined;
  const refused =
    pendings.find((p) => p.state === 'failed' && p.failure !== null) ??
    pendings.find((p) => p.state === 'uncertain');
  v.set({
    kind: 'applyFailed',
    batchId,
    heard: last?.transcript.text ?? '',
    source: pendings.some((p) => p.origin === 'typed') ? 'typed' : 'voice',
    lines: pendings.map((p) => shown(p) ?? v.runner.describe(p.action)),
    reason: refused?.error ?? '',
    retryable: refused?.retryable ?? false,
  });
}

// Try again: the reversed and unsent parts go again as a new batch, from the same preview.
export function retryApply(v: VoiceInternals): void {
  const s = v.state();
  if (s.kind !== 'applyFailed') return;
  const ids = resendIds(v, s);
  if (ids.length === 0) {
    void rest(v, s);
    return;
  }
  v.set({ kind: 'applying', batchId: v.dispatch(ids) });
}

// Send when online: the same parts go to the runner, which keeps them in the outbox while offline, and the sheet
// closes.
export function queueApply(v: VoiceInternals): void {
  const s = v.state();
  if (s.kind !== 'applyFailed') return;
  const ids = resendIds(v, s);
  if (ids.length > 0) v.dispatch(ids);
  void rest(v, s);
}

// Discard: nothing more is sent, and the sheet closes.
export function discardApply(v: VoiceInternals): void {
  const s = v.state();
  if (s.kind !== 'applyFailed') return;
  void rest(v, s);
}

function lineOf(result: Extract<ResolveResult, { ok: true }>) {
  return (p: Pending) => {
    const i = result.actions.findIndex((a) => sameAction(a, p.action));
    return i < 0 ? undefined : result.lines[i];
  };
}

// The preview's line ids of the batch's reversed and unsent parts. They come from the last preview, which is the one
// the failed batch was sent from.
function resendIds(v: VoiceInternals, s: VoiceUiStateOf<'applyFailed'>): string[] {
  const last = v.last();
  if (last === null || !last.result.ok)
    throw new Error('The failed batch has no preview to send again.');
  const line = lineOf(last.result);
  return v.runner
    .pending()
    .filter((p) => p.batchId === s.batchId && applyGroup(p) !== 'stillApplied')
    .map((p) => {
      const found = line(p);
      if (found === undefined)
        throw new Error('The failed batch is no longer the preview it was sent from.');
      return found.id;
    });
}
