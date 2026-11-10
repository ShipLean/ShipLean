import type { Executor, PreviewLine } from '../actions/types';
import type { WorkItemAction } from './actions';

const REVERSIBLE: Record<WorkItemAction['kind'], boolean> = {
  setState: true,
  assign: true,
  addNote: true,
  setHealth: true,
  setStatus: true,
  // An approval, a discussion post, an answer and a status confirmation have no reverse (decisions A.2).
  approve: false,
  discuss: false,
  reply: false,
  confirmStatus: false,
};

// TODO: the real executor sends each kind through @shiplean/core and adds undo and reconcile.
export const workItemExecutor: Executor<WorkItemAction> = {
  area: 'workItem',
  send() {
    throw new Error('The Work item executor does not send yet.');
  },
  describe(a): PreviewLine {
    return { id: a.kind, icon: 'edit', text: a.key, removable: true };
  },
  reversible: (kind) => REVERSIBLE[kind],
};
