import { useState } from 'react';
import type { ProjectDto } from '../../../api/projects';
import type { NewItemKind } from '../../../routes';
import { useNewItemFromLink } from '../useNewItemFromLink';
import { FilterButton } from '../filter/FilterButton';
import { FilterPanel } from '../filter/FilterPanel';
import { NewFeatureDialog } from '../newFeature/NewFeatureDialog';
import { backlogTestIds as ids } from '../testIds';
import { BacklogToolbar } from '../toolbar/BacklogToolbar';
import type { LevelBacklogLevel, LevelColumnId } from './levelColumns';
import { LevelTable } from './LevelTable';
import { LevelTree } from './LevelTree';
import { useLevelBacklog } from './useLevelBacklog';

const FEATURE_KINDS: readonly NewItemKind[] = ['feature'];
const EPIC_KINDS: readonly NewItemKind[] = ['epic'];
const FEATURE_MODES = ['list', 'tree'] as const;

// The Features or Epics backlog: the shared toolbar (with the level picker), the filter, and one row per Feature or Epic
// with its progress, points and, on Epics, its Features count. Features also have Tree mode (the List · Tree switch); Epics are
// always a list.
export function LevelBacklog({
  project,
  level,
  mode = 'list',
}: {
  project: ProjectDto;
  level: LevelBacklogLevel;
  mode?: 'list' | 'tree';
}) {
  const state = useLevelBacklog(project, level, mode);
  const [newFeatureOpen, setNewFeatureOpen] = useState(false);
  useNewItemFromLink(level === 'features' ? FEATURE_KINDS : EPIC_KINDS, (kind) => {
    if (kind === 'feature') setNewFeatureOpen(true);
    else state.setAdding(true);
  });
  const { backlog, view, refused, criteria, lists } = state;

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

  const columns = state.specs.filter((c) => !view.hidden.includes(c.id)).map((c) => c.id);
  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <BacklogToolbar
        projectKey={project.key}
        level={level}
        mode={mode}
        modes={level === 'features' ? FEATURE_MODES : []}
        onNewItem={() => (level === 'features' ? setNewFeatureOpen(true) : state.setAdding(true))}
        newItemLabel={level === 'features' ? '+ New Feature' : undefined}
        includeCompleted={state.includeCompleted}
        onIncludeCompletedChange={state.setIncludeCompleted}
        columns={{ options: state.specs, hidden: view.hidden, onChange: view.setHidden }}
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
      {state.filterOpen ? (
        <FilterPanel
          criteria={criteria}
          onChange={state.setCriteria}
          error={refused}
          lists={lists}
        />
      ) : null}
      {mode === 'tree' ? (
        <LevelTree
          rows={state.rows}
          columns={columns as LevelColumnId[]}
          specs={state.specs}
          projectKey={project.key}
          filtered={state.filtering}
        />
      ) : (
        <LevelTable
          rows={state.rows}
          columns={columns as LevelColumnId[]}
          specs={state.specs}
          level={level}
          projectId={project.id}
          projectKey={project.key}
          adding={state.adding}
          onCloseAdd={() => state.setAdding(false)}
          filtered={state.filtering}
        />
      )}
      {newFeatureOpen ? (
        <NewFeatureDialog project={project} onClose={() => setNewFeatureOpen(false)} />
      ) : null}
    </div>
  );
}
