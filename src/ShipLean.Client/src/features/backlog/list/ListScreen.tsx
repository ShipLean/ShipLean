import { useMemo } from 'react';
import type { ProjectDto } from '../../../api/projects';
import { isStoryLevel } from '../../../api/workItemTypes';
import { NEW_ITEM_KINDS, type NewItemKind } from '../../../routes';
import { AddBelowProvider } from '../add/AddBelowProvider';
import { AddManyButton } from '../add/AddManyButton';
import { PasteHost } from '../add/PasteHost';
import { BulkBar } from '../bulk/BulkBar';
import { BulkOutcomeHost } from '../bulk/BulkOutcomeHost';
import { KeysButton } from '../commands/KeysButton';
import { KeysDialog } from '../commands/KeysDialog';
import { ListKeysHost } from '../commands/ListKeysHost';
import { OpenCommandHost } from '../commands/OpenCommandHost';
import { useReorder } from '../drag/useReorder';
import { BacklogFrame } from '../filter/BacklogFrame';
import type { BacklogFilterState } from '../filter/useBacklogFilter';
import { useReconnectSummary } from '../realtime/useReconnectSummary';
import { useTeammateChanges } from '../realtime/useTeammateChanges';
import { BacklogSearchBox } from '../search/BacklogSearchBox';
import { useBacklogSearch } from '../search/useBacklogSearch';
import { SelectionProvider } from '../selection/SelectionProvider';
import { QueueFixedProvider } from '../strip/QueueFixedProvider';
import { BeforeYouPlan } from '../strip/BeforeYouPlan';
import { useCheckQueue } from '../strip/useCheckQueue';
import { useNewItemFromLink } from '../useNewItemFromLink';
import { ListDnd } from './ListDnd';
import { ListTable } from './ListTable';

const STORY_KINDS: readonly NewItemKind[] = NEW_ITEM_KINDS.filter(isStoryLevel);

// The list screen, inside the providers ListMode mounts: the selection around the shared frame (toolbar, filter, first-run state) around
// the ranked rows (drag to reorder) and the inline new-item row. The check queue narrows the rows first, then the
// search; either one on counts as a filter for what moving and adding mean.
export function ListScreen({ project, state }: { project: ProjectDto; state: BacklogFilterState }) {
  const reorder = useReorder(project.id, state.queryKey);
  const queue = useCheckQueue();
  const search = useBacklogSearch();
  const items = useMemo(() => state.rows.filter((row) => row.kind === 'item'), [state.rows]);
  const queued = useMemo(() => queue.apply(items), [queue, items]);
  const rows = useMemo(() => search.apply(queued), [search, queued]);
  const flashing = useTeammateChanges(project.id, items);
  // Drag stops for a filter, a search or a check queue; adding stops for a filter or a search only.
  const addingOff = state.filtering || search.active;
  const filtering = addingOff || queue.active;
  useReconnectSummary(state.queryKey);
  useNewItemFromLink(STORY_KINDS, (kind) => state.openAdd(kind));

  return (
    <QueueFixedProvider review={state.backlog.data?.review ?? null} queue={queue} queued={queued}>
      <SelectionProvider>
        <ListKeysHost />
        <OpenCommandHost rows={rows} projectKey={project.key} />
        <AddBelowProvider projectId={project.id} projectKey={project.key}>
          <PasteHost projectId={project.id} projectKey={project.key}>
            <BacklogFrame
              project={project}
              mode="list"
              state={state}
              reorder={reorder}
              rowCount={rows.length}
              filtering={filtering}
              options={() => <AddManyButton />}
              search={() => <BacklogSearchBox search={search} />}
              keys={() => <KeysButton />}
              movingOffText="Reordering is off while a filter is on, because rank is relative to items you can't see. # shows each item's place in the full backlog."
            >
              {(columns) => (
                <>
                  <div className="sticky top-0 z-[5] flex-none">
                    <BeforeYouPlan review={state.backlog.data?.review ?? null} queue={queue} />
                  </div>
                  <BulkOutcomeHost projectId={project.id} />
                  <ListDnd
                    rows={rows}
                    filtering={filtering}
                    onReorder={(row, move) => void reorder.drop(row, move.beforeItemId)}
                  >
                    <ListTable
                      rows={rows}
                      columns={columns}
                      projectId={project.id}
                      projectKey={project.key}
                      adding={state.adding}
                      addType={state.addType}
                      addRequest={state.addRequest}
                      onCloseAdd={() => state.setAdding(false)}
                      dragDisabled={filtering}
                      flashing={flashing}
                      filtered={addingOff}
                      searched={search.active ? search.query.trim() : ''}
                    />
                  </ListDnd>
                  <BulkBar
                    mode="list"
                    projectId={project.id}
                    projectKey={project.key}
                    view={state.backlog.data}
                  />
                  <KeysDialog />
                </>
              )}
            </BacklogFrame>
          </PasteHost>
        </AddBelowProvider>
      </SelectionProvider>
    </QueueFixedProvider>
  );
}
