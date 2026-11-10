import type { InboxItemDto } from '@shiplean/core/api/inbox';
import type { FeatureRowDto } from '@shiplean/core/api/featuresOverview';
import type { MemberDto, StateDto } from '@shiplean/core/api/projects';
import type { ScreenVoiceSpec } from '../voice/contracts';
import type { FeaturesAction } from './actions';

export interface FeaturesVoiceContext {
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

// TODO: the Features voice actions and their resolution replace this stub.
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
