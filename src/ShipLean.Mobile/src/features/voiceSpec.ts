import type { InboxItemDto } from '@shiplean/core/api/inbox';
import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import type { MemberDto, StateDto } from '@shiplean/core/api/projects';
import type { ScreenVoiceSpec } from '../voice/contracts';
import type { FeaturesAction } from './actions';

export interface FeaturesVoiceContext {
  projectId: string;
  // True on one Feature's board, where move_rank and filter_open are not offered.
  board: boolean;
  focus: FeatureRowDto | null;
  rows: FeatureRowDto[];
  // In rank order.
  order: FeatureRowDto[];
  states: StateDto[];
  // The open asks per Feature, from the Inbox.
  asks: InboxItemDto[];
  members: MemberDto[];
  projectKey: string;
  // A chip is on: voice rank moves are refused, as the web's isFiltering.
  filtering: boolean;
}

// TODO: resolve to FeaturesAction kinds only, with N9's ResolveResult and its view.
// TODO: rank moves are refused while ctx.filtering, as the web's isFiltering.
// TODO: approve and answer resolve against ctx.asks.
// TODO: note and status text is copied word for word with copyNoteSpan.
// TODO: answers show on screen, never read aloud.
export function featuresVoiceSpec(ctx: FeaturesVoiceContext): ScreenVoiceSpec<FeaturesAction> {
  return {
    screen: 'features',
    actions: [],
    context: () => ({ projectKey: ctx.projectKey }),
    hotwords: () => [],
    toActions: () => ({
      ok: false,
      failure: { kind: 'noValidAction', detail: { action: null, reason: 'none' } },
    }),
  };
}
