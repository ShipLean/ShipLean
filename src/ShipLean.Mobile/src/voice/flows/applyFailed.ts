// A voice batch that failed online: say what stayed applied, what was reversed and what wasn't sent.
import type { Pending } from '../../actions/types';
import type { ResolveResult } from '../contracts';
import type { VoiceInternals, VoiceUiStateOf } from '../internals';
import { rest, sameAction } from './common';

// The runner has already handled the batch (N8, A.2): it stopped at the first refusal and, for a live voice batch,
// reversed newest first the sent actions that have a reverse before it reported the refusal, so every sent part's undo
// outcome is final by now. Each part is in one group. A sent part of a batch the runner didn't reverse (dispatched
// offline, typed, or not all-or-nothing) stayed applied.
export type ApplyGroup = 'stillApplied' | 'reversed' | 'notSent';

export function applyGroup(p: Pending): ApplyGroup {
  if (p.undoOutcome === 'undone') return 'reversed';
  if (p.state === 'sent') return 'stillApplied';
  return 'notSent';
}

// The batch's lines, one per part in the runner's order, each with its part's id and worded as the preview showed it,
// with the reason the runner gave for the part that was refused (or got no answer). A batch refused with a conflict is
// left to onConflict (D17): nothing was applied, and the preview comes back to be confirmed again.
export function onApplyFailed(v: VoiceInternals, batchId: string, pendings: Pending[]): void {
  if (pendings.some((p) => p.failure === 'conflict')) return;
  showApplyFailed(v, batchId, pendings);
}

// The sheet itself, for any refusal: onConflict shows it when the screen never comes to hold the server's item.
export function showApplyFailed(v: VoiceInternals, batchId: string, pendings: Pending[]): void {
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
    lines: pendings.map((p) => ({ ...(shown(p) ?? v.runner.describe(p.action)), id: p.id })),
    reason: refused?.error ?? '',
    retryable: refused?.retryable ?? false,
  });
}

// Try again: a part that got no answer may have landed, so the runner asks the area about it (and sends it, and the
// parts held behind it, only if it didn't). When the runner reversed parts of the batch, the reversed and refused parts
// go again as a new batch from the same preview, which keeps their order. Otherwise (always for a typed batch) the
// runner sends the same failed parts again, so a part never lives on in the outbox beside its resend.
export function retryApply(v: VoiceInternals): void {
  const s = v.state();
  if (s.kind !== 'applyFailed') return;
  const parts = partsOf(v, s);
  for (const p of parts) if (p.state === 'uncertain') v.runner.retry(p.id);
  if (!parts.some(reversed)) {
    retryFailed(v, parts);
    void rest(v, s);
    return;
  }
  const ids = resendIds(v, parts);
  if (ids.length === 0) {
    void rest(v, s);
    return;
  }
  const batchId = v.dispatch(ids);
  retireRefused(v, parts);
  v.set({ kind: 'applying', batchId });
}

// Send when online: as Try again, but the sheet closes at once and the runner keeps what it sends in the outbox while
// offline. A part with no answer and the parts behind it are already in the outbox: the runner settles them when the
// connection comes back.
export function queueApply(v: VoiceInternals): void {
  const s = v.state();
  if (s.kind !== 'applyFailed') return;
  const parts = partsOf(v, s);
  if (parts.some(reversed)) {
    const ids = resendIds(v, parts);
    if (ids.length > 0) {
      v.dispatch(ids);
      retireRefused(v, parts);
    }
  } else retryFailed(v, parts);
  void rest(v, s);
}

// Discard: nothing more of the batch is sent (what waits behind a part with no answer is dropped, and that part is
// dropped too if it didn't land), and the sheet closes.
export function discardApply(v: VoiceInternals): void {
  const s = v.state();
  if (s.kind !== 'applyFailed') return;
  v.runner.discardUnsent(s.batchId);
  void rest(v, s);
}

function lineOf(result: Extract<ResolveResult, { ok: true }>) {
  return (p: Pending) => {
    const i = result.actions.findIndex((a) => sameAction(a, p.action));
    return i < 0 ? undefined : result.lines[i];
  };
}

function partsOf(v: VoiceInternals, s: VoiceUiStateOf<'applyFailed'>): Pending[] {
  return v.runner.pending().filter((p) => p.batchId === s.batchId);
}

const reversed = (p: Pending) => p.state === 'sent' && p.undoOutcome === 'undone';

// The runner queues each failed part again, in the batch's order.
function retryFailed(v: VoiceInternals, parts: Pending[]): void {
  for (const p of parts) if (p.state === 'failed') v.runner.retry(p.id);
}

// Once the new batch is dispatched, the old batch's refused and never-sent parts have gone again in it: the runner
// drops them, so nothing (VoiceSendFailedBanner) goes on saying they didn't send.
function retireRefused(v: VoiceInternals, parts: Pending[]): void {
  v.runner.retireFailed(parts.filter((p) => p.state === 'failed').map((p) => p.id));
}

// Sent again in a new batch: a part the runner reversed, or one it refused or never sent because of the refusal. Never
// a part with no answer or one queued behind it (the runner still holds those), so nothing can land twice.
function resendable(p: Pending): boolean {
  return reversed(p) || p.state === 'failed';
}

// The preview's line ids of the batch's parts to send again. They come from the last preview, which is the one the
// failed batch was sent from.
function resendIds(v: VoiceInternals, batch: Pending[]): string[] {
  const parts = batch.filter(resendable);
  if (parts.length === 0) return [];
  const last = v.last();
  if (last === null || !last.result.ok)
    throw new Error('The failed batch has no preview to send again.');
  const line = lineOf(last.result);
  return parts.map((p) => {
    const found = line(p);
    if (found === undefined)
      throw new Error('The failed batch is no longer the preview it was sent from.');
    return found.id;
  });
}
