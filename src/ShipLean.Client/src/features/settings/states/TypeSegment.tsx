import { useEffect, useRef } from 'react';
import { WORK_ITEM_TYPE_IDS, type WorkItemTypeId } from '@shiplean/core/api/refs';
import { TypeIcon } from '../../../components/TypeIcon';
import { settingsTestIds } from '../testIds';
import { TYPE_LABELS } from './categories';

const testIds: Record<WorkItemTypeId, string> = {
  epic: settingsTestIds.stateTypeEpic,
  feature: settingsTestIds.stateTypeFeature,
  story: settingsTestIds.stateTypeStory,
  bug: settingsTestIds.stateTypeBug,
  task: settingsTestIds.stateTypeTask,
  action: settingsTestIds.stateTypeAction,
  bau: settingsTestIds.stateTypeBau,
};

export const typeLabel = (type: WorkItemTypeId) => TYPE_LABELS[type];

/**
 * Picks which work item type's states are shown, with how many each has. At touch the types are a sideways row of chips
 * that scrolls in its own box, the picked one kept in view (SHIP-1809 P4).
 */
export function TypeSegment({
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
  const row = useRef<HTMLDivElement>(null);
  // Only the row scrolls, sideways: on desktop it never overflows, so this does nothing there.
  useEffect(() => {
    const box = row.current;
    const picked = box?.querySelector<HTMLElement>('[aria-pressed="true"]');
    if (!box || !picked) return;
    if (picked.offsetLeft < box.scrollLeft) box.scrollLeft = picked.offsetLeft;
    else if (picked.offsetLeft + picked.offsetWidth > box.scrollLeft + box.clientWidth)
      box.scrollLeft = picked.offsetLeft + picked.offsetWidth - box.clientWidth;
  }, [active]);
  return (
    <div
      ref={row}
      role="group"
      aria-label="Work item type"
      data-testid={settingsTestIds.stateTypeRow}
      className="flex gap-1 touch:gap-2 touch:overflow-x-auto touch:[scrollbar-width:none]"
    >
      {WORK_ITEM_TYPE_IDS.map((type) => (
        <button
          key={type}
          type="button"
          id={`settings-field-stateType-${type}`}
          data-testid={testIds[type]}
          aria-pressed={type === active}
          onClick={() => onPick(type)}
          className={`inline-flex h-7 items-center gap-1.5 rounded-control border px-2 text-[length:var(--text-sm)] touch:h-[var(--touch-target)] touch:flex-none touch:rounded-full touch:px-3 touch:whitespace-nowrap ${type === active ? 'border-signal bg-panel-sunken text-ink touch:bg-signal-soft touch:font-semibold' : 'border-edge text-ink-muted touch:text-ink'}`}
        >
          <TypeIcon type={type} />
          {TYPE_LABELS[type]}
          <span className="text-ink-muted">{counts[type]}</span>
          {errored.has(type) ? (
            <span
              data-testid={settingsTestIds.stateTypeDot}
              aria-label="Has errors"
              className="size-[7px] rounded-full bg-alert"
            />
          ) : null}
        </button>
      ))}
    </div>
  );
}
