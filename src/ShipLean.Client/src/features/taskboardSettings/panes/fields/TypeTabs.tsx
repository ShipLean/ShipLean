import type { WorkItemTypeId } from '@shiplean/core/api/refs';
import { TypeIcon } from '../../../../components/TypeIcon';
import { taskboardSettingsTestIds as ids } from '../../testIds';

const names: Record<WorkItemTypeId, string> = {
  epic: 'Epic',
  feature: 'Feature',
  story: 'User Story',
  bug: 'Bug',
  task: 'Task',
  action: 'Action item',
  bau: 'BAU',
};

/** Which work item type's card the pane edits. Task cards are the Taskboard's cards; story and bug settings are the row headers'. */
export function TypeTabs({
  types,
  current,
  onSelect,
}: {
  types: WorkItemTypeId[];
  current: WorkItemTypeId;
  onSelect(type: WorkItemTypeId): void;
}) {
  return (
    <div className="flex items-center gap-2 touch:flex-wrap">
      <div
        role="group"
        aria-label="Work item type"
        className="inline-flex overflow-hidden rounded-control border border-edge touch:max-w-full touch:overflow-x-auto"
      >
        {types.map((type) => (
          <button
            key={type}
            type="button"
            data-testid={ids.fieldsTypeTab}
            data-type={type}
            aria-pressed={type === current}
            className="inline-flex items-center gap-1.5 px-2 py-1 text-[length:var(--text-sm)] aria-pressed:bg-accent-soft touch:min-h-[var(--touch-target)] touch:shrink-0"
            onClick={() => onSelect(type)}
          >
            <TypeIcon type={type} />
            {names[type]}
          </button>
        ))}
      </div>
      <span className="text-[length:var(--text-xs)] text-ink-muted">{rowHint(current)}</span>
    </div>
  );
}

function rowHint(type: WorkItemTypeId): string {
  return type === 'task'
    ? 'Settings for task cards'
    : `Settings for ${names[type].toLowerCase()} row headers`;
}
