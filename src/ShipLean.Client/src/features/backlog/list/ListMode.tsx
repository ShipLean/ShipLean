import type { ProjectDto } from '../../../api/projects';
import type { NewItemKind } from '../../../routes';
import { useReorder } from '../drag/useReorder';
import { BacklogFrame } from '../filter/BacklogFrame';
import { useBacklogFilter } from '../filter/useBacklogFilter';
import { useReconnectSummary } from '../realtime/useReconnectSummary';
import { useTeammateChanges } from '../realtime/useTeammateChanges';
import { useNewItemFromLink } from '../useNewItemFromLink';
import { ListTable } from './ListTable';

const STORY_KINDS: readonly NewItemKind[] = ['story', 'bug'];

// The Stories backlog in list mode: the shared frame (toolbar, filter, first-run state) around the ranked rows (drag to
// reorder) and the inline new-item row.
export function ListMode({ project }: { project: ProjectDto }) {
  const state = useBacklogFilter(project, 'list');
  const reorder = useReorder(project.id, state.queryKey);
  const rows = state.rows.filter((row) => row.kind === 'item');
  const flashing = useTeammateChanges(project.id, rows);
  useReconnectSummary(state.queryKey);
  useNewItemFromLink(STORY_KINDS, (kind) => state.openAdd(kind === 'bug' ? 'bug' : 'story'));

  return (
    <BacklogFrame
      project={project}
      mode="list"
      state={state}
      reorder={reorder}
      rowCount={rows.length}
      movingOffText="Reordering is off while a filter is on, because rank is relative to items you can't see. # shows each item's place in the full backlog."
    >
      {(columns) => (
        <ListTable
          rows={rows}
          columns={columns}
          projectId={project.id}
          projectKey={project.key}
          adding={state.adding}
          addType={state.addType}
          addRequest={state.addRequest}
          onCloseAdd={() => state.setAdding(false)}
          dragDisabled={state.filtering}
          flashing={flashing}
          onReorder={(row, move) => void reorder.drop(row, move.beforeItemId)}
          filtered={state.filtering}
        />
      )}
    </BacklogFrame>
  );
}
