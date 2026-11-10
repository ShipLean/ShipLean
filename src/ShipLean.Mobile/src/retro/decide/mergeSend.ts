// Senders for mergeThemes and keepApart (RT3a). A theme merge has no undo.
import type { Api, Pending, PreviewLine } from '../../actions/types';
import type { MergeAction } from '../actions';

export async function sendMerge(a: MergeAction, api: Api): Promise<void> {
  // TODO: mergeThemeProposal or keepThemeProposalApart through the core.
  void api;
  throw new Error(`Sending ${a.kind} is not built yet.`);
}

export function describeMerge(a: MergeAction): PreviewLine {
  // TODO: word each kind as the design's preview rows do.
  return { id: `retro:${a.kind}:${a.proposalId}`, icon: 'dot', text: a.kind, removable: true };
}

export async function reconcileMerge(items: Pending[], api: Api): Promise<('sent' | 'notSent')[]> {
  // TODO: reload the insights and say, per pending, whether the proposal is settled.
  void api;
  return items.map(() => 'notSent');
}
