// The Retro area's action union (N8). Every change on the Retro tab, from a tap, typing or voice, is one of these.
import type { RetroCategory } from '@shiplean/core/api/retros';

export type RetroAction =
  | {
      area: 'retro';
      kind: 'addPoint';
      // The item the point is about (P-R1).
      itemId: string | null;
      // A key alone is allowed; the sender resolves it (E14).
      key: string | null;
      projectId: string;
      // Set when no item is named.
      sprintId: string | null;
      category: RetroCategory;
      categoryGuessed: boolean;
      text: string;
    }
  | { area: 'retro'; kind: 'support' | 'unsupport'; itemId: null; key: null; pointId: string }
  | {
      area: 'retro';
      kind: 'newAction';
      itemId: null;
      key: null;
      projectId: string;
      // The sprint retro the points are in; send and reconcile read it.
      sprintId: string;
      pointIds: string[];
      title: string;
      ownerId: string | null;
      ownerName: string | null;
      themeId: string | null;
    }
  | {
      area: 'retro';
      kind: 'addToAction';
      // The action item.
      itemId: string;
      key: string;
      projectId: string;
      pointIds: string[];
    }
  | {
      area: 'retro';
      kind: 'notNeeded';
      itemId: null;
      key: null;
      projectId: string;
      sprintId: string;
      pointId: string;
      // May be '' (P-R4); send() posts 'Not needed' then, since the server refuses ''.
      reason: string;
    }
  | {
      area: 'retro';
      kind: 'mergeThemes' | 'keepApart';
      itemId: null;
      key: null;
      proposalId: string;
      fromName: string;
      intoName: string;
    };

export type RetroKind = RetroAction['kind'];

// The kinds each sender file takes.
export type PointAction = Extract<RetroAction, { kind: 'addPoint' | 'support' | 'unsupport' }>;
export type DecisionAction = Extract<
  RetroAction,
  { kind: 'newAction' | 'addToAction' | 'notNeeded' }
>;
export type MergeAction = Extract<RetroAction, { kind: 'mergeThemes' | 'keepApart' }>;
