// The Backlog's action union (N8). It imports nothing from another area.
import type { BacklogLevel } from '@shiplean/core/api/backlog';
import type { BulkPlacement } from '@shiplean/core/api/backlogWrites';
import type { RankShift } from '../actions/rankPlan';

export type { BacklogLevel };

export type BacklogAction =
  | {
      area: 'backlog';
      kind: 'move';
      itemId: string;
      key: string;
      revision: number;
      level: BacklogLevel;
      fromRank: number;
      toRank: number;
      placement: BulkPlacement;
      undoPlacement: BulkPlacement;
      shifted: RankShift[];
      warning: string | null;
    }
  | {
      area: 'backlog';
      kind: 'addItem';
      itemId: null;
      key: null;
      projectId: string;
      type: 'story' | 'bug' | 'feature';
      title: string;
      parentId: string | null;
      parentKey: string | null;
      // null = top.
      placement: BulkPlacement | null;
    }
  | { area: 'backlog'; kind: 'approve'; itemId: string; key: string; numbers: number[] }
  | { area: 'backlog'; kind: 'addNote'; itemId: string; key: string; body: string };

export type BacklogMove = Extract<BacklogAction, { kind: 'move' }>;
export type BacklogAddItem = Extract<BacklogAction, { kind: 'addItem' }>;
export type BacklogApprove = Extract<BacklogAction, { kind: 'approve' }>;
export type BacklogAddNote = Extract<BacklogAction, { kind: 'addNote' }>;
