import type { BulkPlacement } from '@shiplean/core/api/backlogWrites';
import type { FeatureHealth } from '@shiplean/core/api/screens';

// The Features screens' actions (N8). Every kind carries what its send, its Undo and its preview need.
export type FeaturesAction = { area: 'features' } & (
  | {
      kind: 'setState';
      itemId: string;
      key: string;
      revision: number;
      stateId: string;
      stateName: string;
      fromStateId: string;
      fromStateName: string;
      closing: boolean;
      // D8: warn when closing a Feature that still has open stories.
      openStories: number;
    }
  | {
      kind: 'setHealth';
      itemId: string;
      key: string;
      revision: number;
      health: FeatureHealth | null;
      from: FeatureHealth | null;
    }
  | { kind: 'markDigestSeen'; itemId: null; key: null; projectId: string }
  | {
      // bulkPlace (P-F3).
      kind: 'moveRank';
      // bulkPlace needs it; send gets only the action.
      projectId: string;
      itemId: string;
      key: string;
      fromRank: number;
      toRank: number;
      placement: BulkPlacement;
      undoPlacement: BulkPlacement;
      shifted: { key: string; from: number; to: number }[];
    }
  // Asks writes.
  | { kind: 'approve'; itemId: string; key: string; numbers: number[] }
  // answerQuestion.
  | { kind: 'answer'; itemId: string; key: string; number: number; body: string }
  // Comments add; Undo archives it.
  | { kind: 'addNote'; itemId: string; key: string; body: string }
  // setStatusLine; Undo PATCHes `from` back.
  | {
      kind: 'setStatus';
      itemId: string;
      key: string;
      revision: number;
      text: string;
      from: string | null;
    }
);
