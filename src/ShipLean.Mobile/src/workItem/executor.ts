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

// TODO: the real executor replaces this stub. send per kind: setState/assign/setHealth patchItem(itemId, revision, changes);
// setStatus setStatusLine(itemId, revision, text); confirmStatus confirmStatus(itemId, revision); addNote addComment(itemId,
// body), resolving { createdId: comment.id }; approve confirmDecisions; discuss discussDecision; reply answerQuestion.
// undo: addNote archiveComment(receipt.createdId), and a null receipt is a failed outcome, never a guess;
// setState/assign/setHealth patch the from* value back; setStatus patches statusLine `from` back (null clears it).
// The send moved the revision, so undo reads the current revision first instead of reusing a.revision. approve,
// discuss, reply and confirmStatus have no undo. reconcile: an uncertain send re-reads the item or its asks; a 409 is
// read with conflictOf(error).current, never retried blind. describe uses words copied exactly (stateName, name, body)
// and no client-made dates.
export const workItemExecutor: Executor<WorkItemAction> = {
  area: 'workItem',
  async send() {
    throw new Error('The Work item executor does not send yet.');
  },
  describe(a): PreviewLine {
    return { id: a.kind, icon: 'edit', text: a.key, removable: true };
  },
  reversible: (kind) => REVERSIBLE[kind],
};
