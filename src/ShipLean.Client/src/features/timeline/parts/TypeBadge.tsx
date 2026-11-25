import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import { timelineTestIds as ids } from '../testIds';
import './timelineTokens.css';

const letters: Record<WorkItemTypeId, string> = {
  epic: 'E',
  feature: 'F',
  story: 'S',
  bug: 'B',
  task: 'T',
  action: 'A',
  bau: 'U',
};
const names: Record<WorkItemTypeId, string> = {
  epic: 'Epic',
  feature: 'Feature',
  story: 'Story',
  bug: 'Bug',
  task: 'Task',
  action: 'Action',
  bau: 'BAU',
};

/** The timeline's own badge colours; any other type keeps its house type colour. */
const colour = (type: WorkItemTypeId): string =>
  type === 'story' || type === 'task' || type === 'bug'
    ? `var(--tl-badge-${type})`
    : `var(--type-${type})`;

/** The 16px square with the type's letter that starts a timeline row (S story, T task, B bug). */
export function TypeBadge({ type }: { type: WorkItemTypeId }) {
  return (
    <span
      data-testid={ids.typeBadge}
      data-type={type}
      role="img"
      aria-label={names[type]}
      title={names[type]}
      className="inline-grid size-4 flex-none place-items-center rounded-[var(--radius-xs)] text-[9px] leading-none font-bold text-signal-ink"
      style={{ background: colour(type) }}
    >
      {letters[type]}
    </span>
  );
}
