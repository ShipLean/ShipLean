import type { WorkItemDto } from '../../../api/workItems';
import { workItemTestIds as ids } from '../testIds';
import { AssigneeField } from './AssigneeField';
import type { Draft, DraftField } from './draft';
import { ParentField } from './ParentField';
import { PointsField } from './PointsField';
import { PriorityField } from './PriorityField';
import { SprintField } from './SprintField';
import { StateField } from './StateField';
import { TagsField } from './TagsField';

// State, Assignee, Sprint, Story points, Priority, Parent and Tags across the full width.
export function FieldsStrip({
  item,
  draft,
  changed,
  onChange,
}: {
  item: WorkItemDto;
  draft: Draft;
  changed: readonly DraftField[];
  onChange: (patch: Partial<Draft>) => void;
}) {
  const isChanged = (field: DraftField) => changed.includes(field);
  return (
    <div
      data-testid={ids.fields}
      aria-label="Fields"
      className="flex flex-wrap items-start gap-x-4 gap-y-2 border-b border-edge bg-panel px-4 py-3"
    >
      <StateField
        projectId={item.projectId}
        type={item.type.id}
        value={draft.state}
        changed={isChanged('state')}
        onChange={(state) => onChange({ state })}
      />
      <AssigneeField
        projectId={item.projectId}
        value={draft.assignee}
        changed={isChanged('assignee')}
        onChange={(assignee) => onChange({ assignee })}
      />
      {item.type.id === 'story' || item.type.id === 'bug' || item.type.id === 'task' ? (
        <SprintField
          projectId={item.projectId}
          value={draft.sprint}
          changed={isChanged('sprint')}
          onChange={(sprint) => onChange({ sprint })}
        />
      ) : null}
      <PointsField
        value={draft.points}
        changed={isChanged('points')}
        onChange={(points) => onChange({ points })}
      />
      <PriorityField
        value={draft.priority}
        changed={isChanged('priority')}
        onChange={(priority) => onChange({ priority })}
      />
      <ParentField
        projectId={item.projectId}
        itemId={item.id}
        type={item.type.id}
        value={draft.parent}
        changed={isChanged('parent')}
        onChange={(parent) => onChange({ parent })}
      />
      <TagsField
        value={draft.tags}
        changed={isChanged('tags')}
        onChange={(tags) => onChange({ tags })}
      />
    </div>
  );
}
