// The Retro executor: delegates by kind to the three sender files.
import type { Api, AppAction, Executor, Pending, PreviewLine } from '../actions/types';
import type { DecisionAction, MergeAction, PointAction, RetroAction, RetroKind } from './actions';
import { describePoint, reconcilePoint, sendPoint, undoPoint } from './add/send';
import { describeDecision, reconcileDecision, sendDecision, undoDecision } from './decide/send';
import { describeMerge, reconcileMerge, sendMerge } from './decide/mergeSend';

type Sender = 'point' | 'decision' | 'merge';

function senderOf(kind: RetroKind): Sender {
  switch (kind) {
    case 'addPoint':
    case 'support':
    case 'unsupport':
      return 'point';
    case 'newAction':
    case 'addToAction':
    case 'notNeeded':
      return 'decision';
    case 'mergeThemes':
    case 'keepApart':
      return 'merge';
  }
}

// Undo archives a new point (archiveRetroPoint, 204), reverses a +1 or a -1, and takes back a Not needed (undoPointNotNeeded).
const REVERSIBLE: readonly RetroKind[] = ['addPoint', 'support', 'unsupport', 'notNeeded'];

export const retroExecutor: Executor<RetroAction> = {
  area: 'retro',
  send(a, api) {
    switch (senderOf(a.kind)) {
      case 'point':
        return sendPoint(a as PointAction, api);
      case 'decision':
        return sendDecision(a as DecisionAction, api);
      case 'merge':
        return sendMerge(a as MergeAction, api);
    }
  },
  describe(a): PreviewLine {
    switch (senderOf(a.kind)) {
      case 'point':
        return describePoint(a as PointAction);
      case 'decision':
        return describeDecision(a as DecisionAction);
      case 'merge':
        return describeMerge(a as MergeAction);
    }
  },
  reversible: (kind) => REVERSIBLE.includes(kind),
  undo(a, api, receipt) {
    switch (senderOf(a.kind)) {
      case 'point':
        return undoPoint(a as PointAction, api, receipt);
      case 'decision':
        return undoDecision(a as DecisionAction, api);
      case 'merge':
        throw new Error(`${a.kind} has no undo.`);
    }
  },
  async reconcile(items: Pending[], api: Api) {
    // E12: one reconcile per sender, over the pendings that sender sent.
    const groups: Record<Sender, number[]> = { point: [], decision: [], merge: [] };
    items.forEach((p, i) => groups[senderOf((p.action as AppAction as RetroAction).kind)].push(i));
    const reconcilers: Record<Sender, (p: Pending[], api: Api) => Promise<('sent' | 'notSent')[]>> =
      { point: reconcilePoint, decision: reconcileDecision, merge: reconcileMerge };
    const result: ('sent' | 'notSent')[] = items.map(() => 'notSent');
    for (const sender of ['point', 'decision', 'merge'] as const) {
      const indexes = groups[sender];
      if (indexes.length === 0) continue;
      const answers = await reconcilers[sender](
        indexes.map((i) => items[i]),
        api,
      );
      indexes.forEach((original, n) => {
        result[original] = answers[n];
      });
    }
    return result;
  },
};
