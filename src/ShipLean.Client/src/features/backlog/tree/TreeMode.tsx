import { useQueryClient } from '@tanstack/react-query';
import type { ProjectDto } from '../../../api/projects';
import type { BacklogRowDto, BacklogViewDto } from '../../../api/views';
import { workItemKey } from '../../workItem/workItemApi';
import { useReorder } from '../drag/useReorder';
import { BacklogFrame } from '../filter/BacklogFrame';
import { useBacklogFilter } from '../filter/useBacklogFilter';
import { useReconnectSummary } from '../realtime/useReconnectSummary';
import { useTeammateChanges } from '../realtime/useTeammateChanges';
import { reparentWrite } from './reparentWrite';
import { TreeTable } from './TreeTable';
import { placeRow, treeTarget, withWritten } from './treeTarget';

// The Stories backlog in tree mode (Show parents): Epics, Features and their stories, with No parent last. Dragging a story
// onto a Feature, a story under another Feature or the No parent group changes its parent.
export function TreeMode({ project }: { project: ProjectDto }) {
  const queryClient = useQueryClient();
  const state = useBacklogFilter(project, 'tree');
  const { queryKey, rows } = state;
  const reorder = useReorder(project.id, queryKey);
  const flashing = useTeammateChanges(
    project.id,
    rows.filter((row) => row.kind === 'item'),
  );
  useReconnectSummary(queryKey);

  function drop(active: BacklogRowDto, over: BacklogRowDto) {
    const target = treeTarget(rows, active, over);
    if (target === null) return;
    // The PATCH bumps the revision: the answer's revision and the new parent go back into the cache at once, so a
    // second drop made before the backlog is read again sends the revision the server holds and compares with the
    // parent the row has now.
    const write = reparentWrite(active.id, target.parentId, target.beforeItemId, (written) =>
      queryClient.setQueryData<BacklogViewDto>(queryKey, (view) =>
        view === undefined ? view : withWritten(view, active.id, written.revision, target.parent),
      ),
    );
    const sent = reorder.drop(active, target.beforeItemId ?? null, write, target.done);
    // useReorder puts the row last or above an item; the tree puts it where the server will (and at its new depth).
    queryClient.setQueryData<BacklogViewDto>(queryKey, (view) =>
      view === undefined ? view : placeRow(view, active.id, target),
    );
    void sent.then(() =>
      queryClient.invalidateQueries({ queryKey: workItemKey(project.id, active.key) }),
    );
  }

  return (
    <BacklogFrame
      project={project}
      mode="tree"
      state={state}
      reorder={reorder}
      rowCount={rows.length}
      movingOffText="Moving items is off while a filter is on, because a place in the tree is relative to items you can't see."
    >
      {(columns) => (
        <TreeTable
          rows={rows}
          columns={columns}
          projectId={project.id}
          projectKey={project.key}
          adding={state.adding}
          onCloseAdd={() => state.setAdding(false)}
          dragDisabled={state.filtering}
          filtered={state.filtering}
          includeCompleted={state.includeCompleted}
          flashing={flashing}
          onDrop={drop}
        />
      )}
    </BacklogFrame>
  );
}
