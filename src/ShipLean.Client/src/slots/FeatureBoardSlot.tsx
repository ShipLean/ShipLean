import { useQuery } from '@tanstack/react-query';
import { useCallback, useMemo, useState } from 'react';
import type { ProjectDto } from '../api/projects';
import type { UserRef } from '../api/refs';
import type { FeatureBoardViewDto } from '../api/views';
import { useFeatureBoard } from '../features/featureBoard/featureBoardApi';
import { featureBoardLayout, featureOfStory } from '../features/featureBoard/featureBoardLayout';
import { FeatureBoardHeader } from '../features/featureBoard/FeatureBoardHeader';
import { FeatureBoardSections } from '../features/featureBoard/FeatureBoardSections';
import { featureBoardTestIds as boardIds } from '../features/featureBoard/testIds';
import { pagesTestIds } from '../features/pages/testIds';
import { CardFeatureContext } from '../features/taskboard/cards/CardFeatureContext';
import { AddStoryBox } from '../features/taskboard/grid/AddStoryBox';
import { TaskboardToolbar } from '../features/taskboard/grid/TaskboardToolbar';
import { TaskboardDnd } from '../features/taskboard/moves/TaskboardDnd';
import type { TaskboardScope } from '../features/taskboard/taskboardScope';
import { useSetParentState, useTaskboardGrouping } from '../features/taskboard/taskboardApi';
import { QueryFailure } from '../pages/QueryFailure';
import { fetchMe, meKey } from '../queries/shared';

export interface FeatureBoardSlotProps {
  project: ProjectDto;
  itemKey: string;
}

/** One Feature's board: the Taskboard's toolbar and grid, scoped to the Feature, under its header and roll-up. */
export function FeatureBoardSlot({ project, itemKey }: FeatureBoardSlotProps) {
  const { query: board, scope } = useFeatureBoard(project.id, itemKey);
  // As on the other pages: the failure states only while there is no data to show.
  if (board.data === undefined || scope === undefined) {
    return board.isError ? (
      <QueryFailure error={board.error} />
    ) : (
      <p data-testid={boardIds.loading} className="p-4 text-ink-muted">
        Loading the Feature board…
      </p>
    );
  }
  return <LoadedBoard project={project} scope={scope} view={board.data} />;
}

function LoadedBoard({
  project,
  scope,
  view,
}: {
  project: ProjectDto;
  scope: TaskboardScope;
  view: FeatureBoardViewDto;
}) {
  const { loaded, grouping, setGrouping } = useTaskboardGrouping();
  const setState = useSetParentState(project.id, scope);
  const meDto = useQuery({ queryKey: meKey, queryFn: fetchMe }).data;
  const me: UserRef | undefined = meDto && {
    id: meDto.id,
    name: meDto.name,
    initials: meDto.initials,
    avatarColour: meDto.avatarColour,
    photoUrl: meDto.photoUrl,
  };
  const [faces, setFaces] = useState<ReadonlySet<string>>(new Set());
  // Section collapse is page state, by Feature id.
  const [closedSections, setClosedSections] = useState<ReadonlySet<string>>(new Set());
  const [adding, setAdding] = useState(false);
  // Rows the user has opened or closed, by row id; every other row follows its default (a completed story starts collapsed).
  const [toggled, setToggled] = useState<ReadonlyMap<string, boolean>>(new Map());

  const people: UserRef[] = view.peopleRows.flatMap((r) => (r.person ? [r.person] : []));
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
  const layout = useMemo(() => featureBoardLayout(view), [view]);
  const features = useMemo(() => featureOfStory(view), [view]);
  const toggleSection = (sectionId: string) =>
    setClosedSections((current) => {
      const next = new Set(current);
      if (!next.delete(sectionId)) next.add(sectionId);
      return next;
    });
  const isCollapsed = (rowId: string, completed: boolean) => toggled.get(rowId) ?? completed;

  return (
    <div data-testid={pagesTestIds.slotFeatureBoard}>
      <FeatureBoardHeader projectKey={project.key} header={view.header} sections={layout} />
      <TaskboardToolbar
        grouping={grouping}
        onGroupingChange={setGrouping}
        people={people}
        faces={faces}
        onFaceToggle={toggleFace}
        onAddStory={() => setAdding(true)}
      />
      {adding ? (
        <AddStoryBox projectId={project.id} scope={scope} onClose={() => setAdding(false)} />
      ) : null}
      {!loaded ? (
        <p data-testid={boardIds.loading} className="p-4 text-ink-muted">
          Loading the Feature board…
        </p>
      ) : (
        <CardFeatureContext.Provider value={features.size > 0 ? features : null}>
          <TaskboardDnd
            projectId={project.id}
            scope={scope}
            view={view}
            grouping={grouping}
            me={me}
            onLanded={onLanded}
          >
            <FeatureBoardSections
              projectKey={project.key}
              projectId={project.id}
              scope={scope}
              view={view}
              grouping={grouping}
              faces={faces}
              me={me}
              closedSections={closedSections}
              onToggleSection={toggleSection}
              isCollapsed={isCollapsed}
              onToggle={(rowId, completed) =>
                setToggled((current) => new Map(current).set(rowId, !isCollapsed(rowId, completed)))
              }
              onSetState={(id, key, revision, stateId) =>
                setState.mutate({ id, key, revision, stateId })
              }
            />
          </TaskboardDnd>
        </CardFeatureContext.Provider>
      )}
    </div>
  );
}
