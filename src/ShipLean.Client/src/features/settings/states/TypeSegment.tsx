import { WORK_ITEM_TYPE_IDS, type WorkItemTypeId } from '../../../api/refs';
import { TypeIcon } from '../../../components/TypeIcon';
import { settingsTestIds } from '../testIds';
import { TYPE_LABELS } from './categories';

const testIds: Record<WorkItemTypeId, string> = {
  epic: settingsTestIds.stateTypeEpic,
  feature: settingsTestIds.stateTypeFeature,
  story: settingsTestIds.stateTypeStory,
  bug: settingsTestIds.stateTypeBug,
  task: settingsTestIds.stateTypeTask,
};

export const typeLabel = (type: WorkItemTypeId) => TYPE_LABELS[type];

/** Picks which work item type's states are shown, with how many each has. */
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
  return (
    <div role="group" aria-label="Work item type" className="flex gap-1">
      {WORK_ITEM_TYPE_IDS.map((type) => (
        <button
          key={type}
          type="button"
          id={`settings-field-stateType-${type}`}
          data-testid={testIds[type]}
          aria-pressed={type === active}
          onClick={() => onPick(type)}
          className={`inline-flex h-7 items-center gap-1.5 rounded-control border px-2 text-[length:var(--text-sm)] ${type === active ? 'border-signal bg-panel-sunken text-ink' : 'border-edge text-ink-muted'}`}
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
