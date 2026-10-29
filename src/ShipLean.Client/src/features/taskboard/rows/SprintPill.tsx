import type { SprintRef } from '../../../api/refs';
import { taskboardTestIds as ids } from '../testIds';

/** Where a story or bug sits on a Feature board: its sprint (the current one tinted), or a dashed Backlog when it has none. */
export function SprintPill({ sprint }: { sprint: SprintRef | null }) {
  if (sprint === null) {
    return (
      <span
        data-testid={ids.rowSprint}
        data-sprint="backlog"
        title="Not in a sprint"
        className="inline-flex h-5 items-center rounded-full border border-dashed border-edge px-[7px] text-[length:var(--text-xs)] whitespace-nowrap text-ink-muted"
      >
        Backlog
      </span>
    );
  }
  const current = sprint.status === 'current';
  return (
    <span
      data-testid={ids.rowSprint}
      data-sprint={sprint.status}
      title={current ? 'Current sprint' : sprint.name}
      className={`inline-flex h-5 items-center gap-1 rounded-full px-[7px] text-[length:var(--text-xs)] whitespace-nowrap text-ink ${
        current ? 'bg-accent-soft' : 'bg-sunken'
      }`}
    >
      ◷ {sprint.name}
    </span>
  );
}
