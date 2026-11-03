import type { ProjectDto } from '../../../../api/projects';
import type { RetroMergeProposalDto } from '../../../../api/retros';
import { MergeProposalRow } from './MergeProposalRow';

/** The proposals agents made to merge two themes, for a person to answer; nothing when there are none. */
export function MergeProposals({
  project,
  proposals,
}: {
  project: ProjectDto;
  proposals: readonly RetroMergeProposalDto[];
}) {
  if (proposals.length === 0) return null;
  return (
    <div
      role="region"
      aria-label="Proposed merges"
      className="rounded-panel border border-edge border-l-[3px] border-l-notice bg-panel px-3 py-2 text-[length:var(--text-sm)] shadow-sm"
    >
      <b className="block">
        {proposals.length} {proposals.length === 1 ? 'merge' : 'merges'} proposed by Decide what to
        do
      </b>
      {proposals.map((proposal) => (
        <MergeProposalRow key={proposal.id} project={project} proposal={proposal} />
      ))}
    </div>
  );
}
