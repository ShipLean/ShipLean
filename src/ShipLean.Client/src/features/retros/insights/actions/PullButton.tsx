import type { SprintDto } from '@shiplean/core/api/sprints';
import { retrosTestIds as ids } from '../../testIds';
import { usePullIntoSprint } from './usePullIntoSprint';

/** The dashed "Pull into Sprint N" button of an action item with no sprint (the design's `.mini`). */
export function PullButton({
  projectId,
  itemKey,
  sprint,
  touch = false,
}: {
  projectId: string;
  itemKey: string;
  sprint: SprintDto;
  /** The phone card's button: 32px, labelled "the next sprint", above the card's link. */
  touch?: boolean;
}) {
  const pull = usePullIntoSprint(projectId);
  return (
    <button
      type="button"
      data-testid={ids.retroActionPull}
      data-item-key={itemKey}
      disabled={pull.isPending}
      className={`cursor-pointer rounded-[var(--radius-xs)] border border-dashed border-edge bg-transparent whitespace-nowrap text-ink-muted hover:border-solid hover:text-ink disabled:cursor-default disabled:opacity-60 ${touch ? 'relative min-h-[var(--touch-target)] px-3 text-[length:var(--text-sm)]' : 'h-5 px-1.5 text-[length:var(--text-2xs)]'}`}
      onClick={() => pull.mutate({ itemKey, sprintId: sprint.id })}
    >
      {touch ? 'Pull into the next sprint' : `Pull into ${sprint.name}`}
    </button>
  );
}
