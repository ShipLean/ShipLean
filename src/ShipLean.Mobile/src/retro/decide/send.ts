// Senders for newAction, addToAction and notNeeded (RT3b).
import type { Api, Pending, PreviewLine } from '../../actions/types';
import type { DecisionAction } from '../actions';

export async function sendDecision(a: DecisionAction): Promise<void> {
  // TODO: newRetroAction (then patchItem for the owner), addPointsToRetroAction, or markPointNotNeeded.
  throw new Error(`Sending ${a.kind} is not built yet.`);
}

export function describeDecision(a: DecisionAction): PreviewLine {
  // TODO: word each kind as the design's preview rows do.
  return { id: `retro:${a.kind}`, icon: 'dot', text: a.kind, removable: true };
}

export async function undoDecision(a: DecisionAction): Promise<void> {
  // TODO: undoPointNotNeeded for notNeeded; the other kinds have no undo.
  throw new Error(`Undoing ${a.kind} is not built yet.`);
}

export async function reconcileDecision(
  items: Pending[],
  api: Api,
): Promise<('sent' | 'notSent')[]> {
  // TODO: reload the points and say, per pending, whether the decision is there.
  void api;
  return items.map(() => 'notSent');
}
