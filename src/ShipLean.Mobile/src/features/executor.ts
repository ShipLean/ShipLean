import type { Executor, PreviewLine } from '../actions/types';
import type { FeaturesAction } from './actions';

const REVERSIBLE: Record<FeaturesAction['kind'], boolean> = {
  setState: true,
  setHealth: true,
  setStatus: true,
  moveRank: true,
  addNote: true,
  // An approval and an answer have no reverse (decisions A.2); marking the digest seen is not a change to undo.
  approve: false,
  answer: false,
  markDigestSeen: false,
};

// TODO: the real executor sends each kind through @shiplean/core and adds undo and reconcile.
export const featuresExecutor: Executor<FeaturesAction> = {
  area: 'features',
  send() {
    throw new Error('The Features executor does not send yet.');
  },
  describe(a): PreviewLine {
    return { id: a.kind, icon: 'edit', text: a.key ?? a.kind, removable: true };
  },
  reversible: (kind) => REVERSIBLE[kind],
};
