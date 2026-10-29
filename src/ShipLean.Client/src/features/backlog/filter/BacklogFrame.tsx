import type { ReactNode } from 'react';
import type { ProjectDto } from '../../../api/projects';
import { BACKLOG_COLUMNS, type BacklogColumnId } from '../backlogApi';
import { ReorderConflict } from '../drag/ReorderConflict';
import type { useReorder } from '../drag/useReorder';
import { EmptyBacklog } from '../list/EmptyBacklog';
import { backlogTestIds as ids } from '../testIds';
import { BacklogToolbar } from '../toolbar/BacklogToolbar';
import { FilterButton } from './FilterButton';
import { FilterPanel } from './FilterPanel';
import type { BacklogFilterState } from './useBacklogFilter';

/**
 * The frame the Stories backlog's list and tree modes share: the toolbar, the load and error states, the filter panel,
 * the banner that says moving is off while a filter is on, the conflict banner and the first-run state. The mode's own
 * table is `children`, given the columns to show.
 */
export function BacklogFrame({
  project,
  mode,
  state,
  reorder,
  movingOffText,
  rowCount,
  children,
}: {
  project: ProjectDto;
  mode: 'list' | 'tree';
  state: BacklogFilterState;
  reorder: ReturnType<typeof useReorder>;
  movingOffText: string;
  rowCount: number;
  children: (columns: BacklogColumnId[]) => ReactNode;
}) {
  const { backlog, view, refused, criteria, filtering, lists } = state;
  const toolbar = (
    <BacklogToolbar
      projectKey={project.key}
      level="stories"
      mode={mode}
      onNewItem={() => state.setAdding(true)}
      includeCompleted={state.includeCompleted}
      onIncludeCompletedChange={state.setIncludeCompleted}
      columns={{ options: BACKLOG_COLUMNS, hidden: view.hidden, onChange: view.setHidden }}
      filter={() => (
        <FilterButton
          criteria={criteria}
          open={state.filterOpen}
          onToggle={state.toggleFilter}
          onRemoveClause={(index) =>
            state.setCriteria({
              ...criteria,
              clauses: criteria.clauses.filter((_, i) => i !== index),
            })
          }
          lists={lists}
        />
      )}
    />
  );

  if ((backlog.isError && refused === null) || view.failed) {
    return (
      <section data-testid={ids.loadFailed} className="p-6">
        <h1 className="page-title">Could not load the backlog</h1>
        <p className="text-ink-muted">Reload the page to try again.</p>
      </section>
    );
  }
  if ((!backlog.isSuccess && refused === null) || !view.loaded) {
    return (
      <div data-testid={ids.loading} className="p-4 text-ink-muted">
        Loading the backlog
      </div>
    );
  }

  const firstRun = rowCount === 0 && project.nextItemNumber === 1 && !state.adding && !filtering;
  const columns = BACKLOG_COLUMNS.filter((c) => !view.hidden.includes(c.id)).map((c) => c.id);
  return (
    <div className="flex min-h-0 flex-1 flex-col">
      {toolbar}
      {state.filterOpen ? (
        <FilterPanel
          criteria={criteria}
          onChange={state.setCriteria}
          error={refused}
          lists={lists}
        />
      ) : null}
      {filtering ? (
        <div
          data-testid={ids.dragOffBanner}
          role="status"
          className="flex-none border-b border-edge px-[var(--space-4)] py-1.5 text-[length:var(--text-sm)]"
          style={{ borderLeft: '3px solid var(--color-info)' }}
        >
          {movingOffText}
        </div>
      ) : null}
      {reorder.conflict ? (
        <ReorderConflict
          conflict={reorder.conflict}
          onUseMine={() => void reorder.useMine()}
          onKeepTheirs={() => void reorder.keepTheirs()}
        />
      ) : null}
      {firstRun ? (
        <EmptyBacklog
          projectKey={project.key}
          onCreate={() => state.setAdding(true)}
          noSprints={lists.sprints?.length === 0}
        />
      ) : (
        children(columns)
      )}
    </div>
  );
}
