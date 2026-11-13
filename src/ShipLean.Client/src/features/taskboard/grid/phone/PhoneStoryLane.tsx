import type { SprintRef } from '@shiplean/core/api/refs';
import type { TaskboardRowDto } from '@shiplean/core/api/views';
import { AddTaskBox } from '../../rows/AddTaskBox';
import { ALL_FIELDS } from '../../rows/StoryRow';
import { StoryRowHeader } from '../../rows/StoryRowHeader';
import { TaskCell } from '../../rows/TaskCell';
import { UnparentedHeader } from '../../rows/UnparentedHeader';
import type { TaskboardGridView } from '../../taskboardGridView';
import type { TaskboardScope } from '../../taskboardScope';
import { taskboardTestIds as ids } from '../../testIds';
import type { StoryLane } from './phoneLanes';

/** A story's lane on one state's page: its compact card, then its tasks in this state, None, or how many the filter hides. */
export function PhoneStoryLane({
  projectKey,
  projectId,
  scope,
  view,
  lane,
  stateId,
  first,
  collapsed,
  onToggle,
  onSetState,
}: {
  projectKey: string;
  projectId: string;
  scope: TaskboardScope;
  view: TaskboardGridView;
  lane: StoryLane<TaskboardRowDto & { sprint?: SprintRef | null }>;
  stateId: string;
  /** The first page: the one with + Task. */
  first: boolean;
  collapsed: boolean;
  onToggle: () => void;
  onSetState: (stateId: string) => void;
}) {
  const { row, rowId, tasks, hidden } = lane;
  const parent = row.parent;
  const resolved = view.parentStates
    .find((s) => s.type === parent?.type.id)
    ?.states.find((state) => state.category === 'resolved');
  return (
    <div
      data-testid={ids.lane}
      data-row={rowId}
      data-lane="story"
      data-collapsed={collapsed}
      className="grid min-w-0 grid-cols-[minmax(0,1fr)]"
    >
      {parent ? (
        <StoryRowHeader
          compact
          projectKey={projectKey}
          parent={parent}
          parentStates={view.parentStates}
          fields={view.cardFields.find((f) => f.type === parent.type.id) ?? ALL_FIELDS}
          showProgress={view.annotations.tasks}
          collapsed={collapsed}
          laneCount={tasks.length}
          offerResolve={row.offerResolve}
          onToggle={onToggle}
          onSetState={onSetState}
          onMoveToResolved={resolved ? () => onSetState(resolved.id) : null}
        />
      ) : (
        <UnparentedHeader
          count={row.cells.reduce((n, c) => n + c.tasks.length, 0)}
          collapsed={collapsed}
          onToggle={onToggle}
        />
      )}
      {collapsed ? null : (
        <TaskCell
          lane
          rowId={rowId}
          stateId={stateId}
          cards={tasks}
          view={view}
          showParent={false}
          empty={
            hidden > 0 ? (
              <span
                data-testid={ids.laneHidden}
                className="px-1 text-[length:var(--text-xs)] text-ink-muted"
              >
                {hidden} hidden by Filter
              </span>
            ) : (
              <span
                data-testid={ids.laneNone}
                className="px-1 text-[length:var(--text-sm)] text-ink-muted"
              >
                None
              </span>
            )
          }
        >
          {first ? (
            <AddTaskBox projectId={projectId} scope={scope} parentId={parent?.id ?? null} />
          ) : null}
        </TaskCell>
      )}
    </div>
  );
}
