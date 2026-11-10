import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import type { ParentRefDto } from '@shiplean/core/api/workItems';
import { ParentCandidates } from '../../workItem/form/ParentCandidates';
import { backlogTestIds as ids } from '../testIds';

/** The parents allowed for every selected type, or a line saying none is. */
export function ParentMenuBody({
  projectId,
  types,
  onPick,
}: {
  projectId: string;
  types: WorkItemTypeId[];
  onPick: (parent: ParentRefDto | null) => void;
}) {
  if (types.length === 0) {
    return (
      <p
        data-testid={ids.bulkMenuOption}
        className="max-w-64 px-2 py-1 text-[length:var(--text-sm)] text-ink-muted"
      >
        No parent type is allowed for every selected type.
      </p>
    );
  }
  return (
    <ParentCandidates projectId={projectId} itemId="" types={types} value={null} onPick={onPick} />
  );
}
