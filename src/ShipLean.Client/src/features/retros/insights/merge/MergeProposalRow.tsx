import { useMutation, useQueryClient } from '@tanstack/react-query';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { RetroMergeProposalDto } from '@shiplean/core/api/retros';
import { keepThemeProposalApart, mergeThemeProposal } from '@shiplean/core/api/retrosApi';
import { useTouchShell } from '../../../../touch/useTouchShell';
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
  const { touch } = useTouchShell();
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
  const pill = 'rounded-full bg-sunken px-2 py-0.5 text-[length:var(--text-xs)] font-medium';
  const answer = touch ? 'min-h-[var(--touch-target)] flex-1 ' : '';
  return (
    <div
      data-testid={ids.retroMergeProposal}
      data-proposal-id={proposal.id}
      className={
        touch
          ? 'grid gap-2 pt-2 text-[length:var(--text-sm)]'
          : 'flex flex-wrap items-center gap-2 pt-1.5 text-[length:var(--text-sm)]'
      }
    >
      <span className={touch ? 'flex flex-wrap items-center gap-2' : 'contents'}>
        <span className={pill}>{proposal.from.name}</span>
        <span className="text-ink-muted">into</span>
        <span className={pill}>{proposal.into.name}</span>
      </span>
      <span className="text-ink-muted">
        {points}, {proposal.reason} · {by} · {proposal.proposedBy.role}
      </span>
      {touch ? null : <span className="flex-1" />}
      <span className={touch ? 'flex gap-2' : 'contents'}>
        <button
          type="button"
          data-testid={ids.retroMergeProposalKeep}
          disabled={busy}
          className={`${answer}cursor-pointer rounded-control border border-edge bg-panel px-2.5 py-0.5 disabled:opacity-50`}
          onClick={() => keep.mutate()}
        >
          Keep apart
        </button>
        <button
          type="button"
          data-testid={ids.retroMergeProposalMerge}
          disabled={busy}
          className={`${answer}cursor-pointer rounded-control border border-signal bg-signal px-2.5 py-0.5 text-signal-ink disabled:opacity-50`}
          onClick={() => merge.mutate()}
        >
          Merge
        </button>
      </span>
    </div>
  );
}
