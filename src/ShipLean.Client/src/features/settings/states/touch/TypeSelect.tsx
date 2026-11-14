import { WORK_ITEM_TYPE_IDS, type WorkItemTypeId } from '@shiplean/core/api/refs';
import { settingsTestIds } from '../../testIds';
import { TYPE_LABELS } from '../categories';

/** The work item type picker at 900px and below: a native select, with each type's count and error mark as text. */
export function TypeSelect({
  active,
  counts,
  errored,
  onPick,
}: {
  active: WorkItemTypeId;
  counts: Record<WorkItemTypeId, number>;
  /** Types with a problem Save found. */
  errored: ReadonlySet<WorkItemTypeId>;
  onPick(type: WorkItemTypeId): void;
}) {
  return (
    <select
      data-testid={settingsTestIds.stateTypeSelect}
      aria-label="Work item type"
      className="hidden h-[var(--touch-target)] w-full rounded-control border border-edge bg-panel px-2 text-ink touch:block"
      value={active}
      onChange={(e) => {
        const next = WORK_ITEM_TYPE_IDS.find((t) => t === e.target.value);
        if (next !== undefined) onPick(next);
      }}
    >
      {WORK_ITEM_TYPE_IDS.map((type) => (
        <option key={type} value={type}>
          {TYPE_LABELS[type]} ({counts[type]}){errored.has(type) ? ' •' : ''}
        </option>
      ))}
    </select>
  );
}
