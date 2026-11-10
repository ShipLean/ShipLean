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

// TODO: send each kind through @shiplean/core only: setState and setHealth with patchItem; setStatus with setStatusLine;
// TODO: moveRank with bulkPlace (position only, changes null); approve with confirmDecisions; answer with answerQuestion;
// TODO: addNote with addComment, body word for word; markDigestSeen with markDigestSeen(projectId).
// TODO: undo: setState PATCHes fromStateId back; setHealth PATCHes `from`; setStatus PATCHes `from` back (null clears it);
// TODO: moveRank bulkPlaces undoPlacement; addNote archives the comment whose id is the send receipt's createdId (amendment 8).
// TODO: approve, answer and markDigestSeen have no reverse, and Undo says they stayed (N8).
// TODO: a stale revision is a 409 read with conflictOf and handled in reconcile(items, api).
// TODO: a 400 on statusLine is shown, never trimmed or reworded.
// TODO: describe() shows the D8 warning when closing && openStories > 0.
// TODO: after a write, invalidate featuresKeys.root(projectId) and featureBoardKeys.board(projectId, key).
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
