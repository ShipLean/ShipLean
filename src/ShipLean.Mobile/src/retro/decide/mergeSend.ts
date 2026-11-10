// Senders for mergeThemes and keepApart (RT3a). Neither has an undo: no route takes a merge or a keep-apart back.
import { listProjects } from '@shiplean/core/api/projects';
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
// per project and the action does not name its project, so the session's projects are read once each (range '6',
// the range the Retro tab reads) and a proposal still listed in any of them is notSent.
export async function reconcileMerge(items: Pending[], api: Api): Promise<('sent' | 'notSent')[]> {
  void api;
  const projects = await listProjects();
  const insights = await Promise.all(projects.map((p) => fetchRetroInsights(p.id, '6')));
  const pending = new Set(insights.flatMap((i) => i.mergeProposals.map((m) => m.id)));
  return items.map((p) => (pending.has((p.action as MergeAction).proposalId) ? 'notSent' : 'sent'));
}
