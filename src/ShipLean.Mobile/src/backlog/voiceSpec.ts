import type { MemberDto } from '@shiplean/core/api/projects';
import type { BacklogRowDto } from '@shiplean/core/api/views';
import type { ScreenVoiceSpec } from '../voice/contracts';
import type { BacklogAction, BacklogLevel } from './actions';

export interface BacklogVoiceContext {
  projectId: string;
  level: BacklogLevel;
  // The visible rows.
  rows: BacklogRowDto[];
  // The level's rows in rank order.
  order: BacklogRowDto[];
  filters: { needsMe: boolean; open: boolean; epicKey: string | null; featureKey: string | null };
  // The parents at the Stories level.
  features: { id: string; key: string; title: string }[];
  members: MemberDto[];
}

// TODO: the Backlog's voice actions and their resolution replace this stub.
export function backlogVoiceSpec(ctx: BacklogVoiceContext): ScreenVoiceSpec<BacklogAction> {
  return {
    screen: 'backlog',
    actions: [],
    context: () => ({ level: ctx.level }),
    hotwords: () => [],
    toActions: () => ({
      ok: false,
      failure: { kind: 'noValidAction', detail: { action: null, reason: 'none' } },
    }),
  };
}
