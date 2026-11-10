import type { FeatureHealth } from '@shiplean/core/api/screens';

// The Work item screen's actions (N8). Every kind carries what its send, its Undo and its preview need.
export type WorkItemAction = { area: 'workItem'; itemId: string; key: string } & (
  | {
      kind: 'setState';
      revision: number;
      stateId: string;
      stateName: string;
      fromStateId: string;
      fromStateName: string;
    }
  | {
      kind: 'assign';
      revision: number;
      userId: string | null;
      name: string | null;
      fromUserId: string | null;
      fromName: string | null;
    }
  // Comments add; Undo archives the note.
  | { kind: 'addNote'; body: string }
  // Asks writes: confirmDecisions.
  | { kind: 'approve'; numbers: number[] }
  // discussDecision.
  | { kind: 'discuss'; number: number; body: string }
  // answerQuestion.
  | { kind: 'reply'; number: number; body: string }
  | {
      kind: 'setHealth';
      revision: number;
      health: FeatureHealth | null;
      from: FeatureHealth | null;
    }
  // Status writes: confirmStatus.
  | { kind: 'confirmStatus'; revision: number }
  // setStatusLine; Undo PATCHes `from` back.
  | { kind: 'setStatus'; revision: number; text: string; from: string | null }
);
