import { useQuery } from '@tanstack/react-query';
import { useCallback, useMemo, useState } from 'react';
import type { ProjectDto } from '@shiplean/core/api/projects';
import type { UserRef } from '@shiplean/core/api/refs';
import type { SprintDto } from '@shiplean/core/api/sprints';
import { fetchMe, meKey } from '../queries/shared';
import type { SheetAction } from '../touch/ActionSheet';
import { collapseAllMap } from '../features/taskboard/grid/boardRowIds';
import { useOpenCardSettings } from '../features/taskboard/grid/useOpenCardSettings';
import { faceCounts } from '../features/taskboard/grid/faceFilter';
import { AddStoryBox } from '../features/taskboard/grid/AddStoryBox';
import { TaskboardGrid } from '../features/taskboard/grid/TaskboardGrid';
import { TaskboardDnd } from '../features/taskboard/moves/TaskboardDnd';
import type { TaskboardScope } from '../features/taskboard/taskboardScope';
import { TaskboardToolbar } from '../features/taskboard/grid/TaskboardToolbar';
import { pagesTestIds } from '../features/pages/testIds';
import {
  useSetParentState,
  useTaskboard,
  useTaskboardGrouping,
} from '../features/taskboard/taskboardApi';
import { taskboardTestIds as ids } from '../features/taskboard/testIds';

export interface TaskboardSlotProps {
  project: ProjectDto;
  sprint: SprintDto;
  sprints: SprintDto[];
}

export function TaskboardSlot({ project, sprint }: TaskboardSlotProps) {
  const board = useTaskboard(project.id, sprint.id);
  const { loaded, grouping, setGrouping } = useTaskboardGrouping();
  const scope = useMemo<TaskboardScope>(
    () => ({ kind: 'sprint', sprintId: sprint.id }),
    [sprint.id],
  );
  const setState = useSetParentState(project.id, scope);
  const openCardSettings = useOpenCardSettings();
  const meDto = useQuery({ queryKey: meKey, queryFn: fetchMe }).data;
  const me: UserRef | undefined = meDto && {
    id: meDto.id,
    name: meDto.name,
    initials: meDto.initials,
    avatarColour: meDto.avatarColour,
    photoUrl: meDto.photoUrl,
  };
  const [faces, setFaces] = useState<ReadonlySet<string>>(new Set());
  const [adding, setAdding] = useState(false);
  // Rows the user has opened or closed, by row id; every other row follows its default (a completed story starts collapsed).
  const [toggled, setToggled] = useState<ReadonlyMap<string, boolean>>(new Map());

  const people: UserRef[] = (board.data?.peopleRows ?? []).flatMap((r) =>
    r.person ? [r.person] : [],
  );
  const toggleFace = (id: string) =>
    setFaces((current) => {
      const next = new Set(current);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  // Stable, so the cards' drag refs are not re-attached on every render.
  const onLanded = useCallback(
    (rowId: string) => setToggled((current) => new Map(current).set(rowId, false)),
    [],
  );
  const view = board.data;
  const more: SheetAction[] = [
    {
      id: 'collapse-all',
      label: 'Collapse all',
      disabled: !view,
      onSelect: () => view && setToggled(collapseAllMap(view, true)),
    },
    {
      id: 'expand-all',
      label: 'Expand all',
      disabled: !view,
      onSelect: () => view && setToggled(collapseAllMap(view, false)),
    },
    {
      id: 'taskboard-settings',
      label: 'Taskboard settings',
      onSelect: openCardSettings,
    },
  ];
  const isCollapsed = (rowId: string, completed: boolean) => toggled.get(rowId) ?? completed;

  return (
    <div data-testid={pagesTestIds.slotTaskboard}>
      <TaskboardToolbar
        grouping={grouping}
        onGroupingChange={setGrouping}
        people={people}
        faces={faces}
        faceCounts={view ? faceCounts(view, grouping) : new Map()}
        onFaceToggle={toggleFace}
        onFacesClear={() => setFaces(new Set())}
        more={more}
        onAddStory={() => setAdding(true)}
      />
      {adding ? (
        <AddStoryBox projectId={project.id} scope={scope} onClose={() => setAdding(false)} />
      ) : null}
      {board.isPending || !loaded ? (
        <p data-testid={ids.loading} className="p-4 text-ink-muted">
          Loading the Taskboard…
        </p>
      ) : board.isError ? (
        <p data-testid={ids.loadFailed} role="alert" className="p-4 text-alert">
          The Taskboard could not be loaded.
        </p>
      ) : (
        <TaskboardDnd
          projectId={project.id}
          scope={scope}
          view={board.data}
          grouping={grouping}
          me={me}
          onLanded={onLanded}
        >
          <TaskboardGrid
            projectKey={project.key}
            projectId={project.id}
            scope={scope}
            view={board.data}
            rows={board.data.rows}
            grouping={grouping}
            faces={faces}
            me={me}
            isCollapsed={isCollapsed}
            onToggle={(rowId, completed) =>
              setToggled((current) => new Map(current).set(rowId, !isCollapsed(rowId, completed)))
            }
            onSetState={(id, key, revision, stateId) =>
              setState.mutate({ id, key, revision, stateId })
            }
          />
        </TaskboardDnd>
      )}
    </div>
  );
}
