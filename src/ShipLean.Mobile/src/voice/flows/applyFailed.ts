// A voice batch that failed online: say what stayed applied, what was reversed and what wasn't sent.
import type { Pending } from '../../actions/types';
import type { VoiceInternals } from '../internals';

// TODO: show the apply-failed sheet from the batch's pendings and their undo outcomes.
export function onApplyFailed(v: VoiceInternals, batchId: string, pendings: Pending[]): void {
  void v;
  void batchId;
  void pendings;
}

// TODO: send the failed batch again.
export function retryApply(v: VoiceInternals): void {
  void v;
}

// TODO: keep the failed batch on the phone and send it when back online.
export function queueApply(v: VoiceInternals): void {
  void v;
}

// TODO: drop the failed batch.
export function discardApply(v: VoiceInternals): void {
  void v;
}
