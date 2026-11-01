import type { SprintDto } from '../../../../api/sprints';
import { retrosTestIds as ids } from '../../testIds';
import { usePullIntoSprint } from './usePullIntoSprint';

/** The dashed "Pull into Sprint N" button of an action item with no sprint (the design's `.mini`). */
export function PullButton({
  projectId,
  itemKey,
  sprint,
}: {
  projectId: string;
  itemKey: string;
  sprint: SprintDto;
}) {
  const pull = usePullIntoSprint(projectId);
  return (
    <button
      type="button"
      data-testid={ids.retroActionPull}
      data-item-key={itemKey}
      disabled={pull.isPending}
      className="h-5 cursor-pointer rounded-[var(--radius-xs)] border border-dashed border-edge bg-transparent px-1.5 text-[length:var(--text-2xs)] whitespace-nowrap text-ink-muted hover:border-solid hover:text-ink disabled:cursor-default disabled:opacity-60"
      onClick={() => pull.mutate({ itemKey, sprintId: sprint.id })}
    >
      Pull into {sprint.name}
    </button>
  );
}
