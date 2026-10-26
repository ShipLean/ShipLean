import type { SprintRef } from '../../../api/refs';
import { Chip } from '../../../components/Chip';
import { backlogTestIds as ids } from '../testIds';

// The sprint a scheduled item is in; the current sprint's chip is accent.
export function SprintCell({ sprint }: { sprint: SprintRef | null }) {
  if (sprint === null) return <span className="text-ink-muted">–</span>;
  return (
    <span data-testid={ids.sprintChip} data-status={sprint.status}>
      <Chip tone={sprint.status === 'current' ? 'accent' : 'muted'}>{sprint.name}</Chip>
    </span>
  );
}
