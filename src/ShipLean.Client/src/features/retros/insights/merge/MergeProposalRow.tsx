import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { ProjectDto } from '../../../../api/projects';
import type { RetroMergeProposalDto } from '../../../../api/retros';
import { keepThemeProposalApart, mergeThemeProposal } from '../../../../api/retrosApi';
import { showToast } from '../../../../components/toastStore';
import { retrosTestIds as ids } from '../../testIds';
import { retrosRootKey } from '../../useRetroQueries';

/** One proposal: the two themes, why, who proposed it, and the two answers. The row goes when the refetch no longer lists it. */
export function MergeProposalRow({
  project,
  proposal,
}: {
  project: ProjectDto;
  proposal: RetroMergeProposalDto;
}) {
  const queryClient = useQueryClient();
  const useAnswer = (send: (proposalId: string) => Promise<void>) =>
    useMutation({
      mutationFn: () => send(proposal.id),
      onSuccess: () => queryClient.invalidateQueries({ queryKey: retrosRootKey(project.id) }),
      onError: (failure) => showToast(failure.message, { tone: 'danger' }),
    });
  const merge = useAnswer(mergeThemeProposal);
  const keep = useAnswer(keepThemeProposalApart);
  const busy = merge.isPending || keep.isPending;
  const points = `${proposal.pointCount} ${proposal.pointCount === 1 ? 'point' : 'points'}`;
  const by = proposal.proposedBy.agentName ?? proposal.proposedBy.user.name;
  return (
    <div
      data-testid={ids.retroMergeProposal}
      data-proposal-id={proposal.id}
      className="flex flex-wrap items-center gap-2 pt-1.5 text-[length:var(--text-sm)]"
    >
      <span className="rounded-full bg-sunken px-2 py-0.5 text-[length:var(--text-xs)] font-medium">
        {proposal.from.name}
      </span>
      <span className="text-ink-muted">into</span>
      <span className="rounded-full bg-sunken px-2 py-0.5 text-[length:var(--text-xs)] font-medium">
        {proposal.into.name}
      </span>
      <span className="text-ink-muted">
        {points}, {proposal.reason} · {by} · {proposal.proposedBy.role}
      </span>
      <span className="flex-1" />
      <button
        type="button"
        data-testid={ids.retroMergeProposalKeep}
        disabled={busy}
        className="cursor-pointer rounded-control border border-edge bg-panel px-2.5 py-0.5 disabled:opacity-50"
        onClick={() => keep.mutate()}
      >
        Keep apart
      </button>
      <button
        type="button"
        data-testid={ids.retroMergeProposalMerge}
        disabled={busy}
        className="cursor-pointer rounded-control border border-signal bg-signal px-2.5 py-0.5 text-signal-ink disabled:opacity-50"
        onClick={() => merge.mutate()}
      >
        Merge
      </button>
    </div>
  );
}
