// Senders for mergeThemes and keepApart (RT3a). Neither has an undo: no route takes a merge or a keep-apart back.
import {
  fetchRetroInsights,
  keepThemeProposalApart,
  mergeThemeProposal,
} from '@shiplean/core/api/retrosApi';
import type { Api, Pending, PreviewLine } from '../../actions/types';
import type { MergeAction } from '../actions';

// A 409 means the proposal is no longer pending; it is thrown as the core's error and the runner shows it.
export async function sendMerge(a: MergeAction, api: Api): Promise<void> {
  void api;
  return a.kind === 'mergeThemes'
    ? mergeThemeProposal(a.proposalId)
    : keepThemeProposalApart(a.proposalId);
}

export function describeMerge(a: MergeAction): PreviewLine {
  return {
    id: `retro:${a.kind}:${a.proposalId}`,
    icon: 'dot',
    text:
      a.kind === 'mergeThemes'
        ? `Merge ${a.fromName} into ${a.intoName}`
        : `Keep ${a.fromName} and ${a.intoName} apart`,
    removable: true,
  };
}

// An uncertain send went through when its proposal is no longer pending. The insights read lists pending proposals
// per project, so each distinct project among the items is read once (range '6', the range the Retro tab reads).
export async function reconcileMerge(items: Pending[], api: Api): Promise<('sent' | 'notSent')[]> {
  void api;
  const actions = items.map((p) => p.action as MergeAction);
  const projectIds = [...new Set(actions.map((a) => a.projectId))];
  const reads = await Promise.all(
    projectIds.map(async (id) => {
      const insights = await fetchRetroInsights(id, '6');
      return [id, new Set(insights.mergeProposals.map((m) => m.id))] as const;
    }),
  );
  const pending = new Map(reads);
  return actions.map((a) => (pending.get(a.projectId)?.has(a.proposalId) ? 'notSent' : 'sent'));
}
