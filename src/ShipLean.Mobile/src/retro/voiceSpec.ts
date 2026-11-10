import type {
  RetroCategory,
  RetroMergeProposalDto,
  RetroPointDto,
} from '@shiplean/core/api/retros';
import type { MemberDto } from '@shiplean/core/api/projects';
import type { ScreenVoiceSpec } from '../voice/contracts';
import type { RetroAction } from './actions';

export type RetroVoiceContext = {
  sprint: { id: string; name: string } | null;
  category: RetroCategory | null;
  points: RetroPointDto[];
  lastTouchedId: string | null;
  openActions: { workItemId: string; key: string; title: string }[];
  proposals: RetroMergeProposalDto[];
  members: MemberDto[];
  projectId: string;
  projectKey: string;
};

// TODO: the Retro voice actions, grammar and resolver replace this stub.
export function retroVoiceSpec(ctx: RetroVoiceContext): ScreenVoiceSpec<RetroAction> {
  void ctx;
  return {
    screen: 'retro',
    actions: [],
    context: () => ({}),
    hotwords: () => [],
    toActions: () => ({ ok: false, failure: { kind: 'nothingHeard', detail: {} } }),
  };
}
