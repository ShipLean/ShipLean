import type { UserRef } from '../../api/refs';
import type { FeatureBoardViewDto } from '../../api/views';
import { TaskboardGrid } from '../taskboard/grid/TaskboardGrid';
import type { TaskboardGrouping } from '../taskboard/taskboardApi';
import type { TaskboardScope } from '../taskboard/taskboardScope';

/** The board's grid. One grid over every row for now; a parent Feature's per-Feature sections are drawn here. */
export function FeatureBoardSections({
  projectKey,
  projectId,
  scope,
  view,
  grouping,
  faces,
  me,
  isCollapsed,
  onToggle,
  onSetState,
}: {
  projectKey: string;
  projectId: string;
  scope: TaskboardScope;
  view: FeatureBoardViewDto;
  grouping: TaskboardGrouping;
  faces: ReadonlySet<string>;
  me: UserRef | undefined;
  isCollapsed: (rowId: string, completed: boolean) => boolean;
  onToggle: (rowId: string, completed: boolean) => void;
  onSetState: (parentId: string, key: string, revision: number, stateId: string) => void;
}) {
  return (
    <TaskboardGrid
      projectKey={projectKey}
      projectId={projectId}
      scope={scope}
      view={view}
      rows={view.rows}
      grouping={grouping}
      faces={faces}
      me={me}
      isCollapsed={isCollapsed}
      onToggle={onToggle}
      onSetState={onSetState}
    />
  );
}
