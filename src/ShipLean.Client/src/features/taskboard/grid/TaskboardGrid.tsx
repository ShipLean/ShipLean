import type { UserRef } from '../../../api/refs';
import type { TaskboardRowDto } from '../../../api/views';
import { StateDot } from '../../../components/StateDot';
import { PersonRow } from '../rows/PersonRow';
import { StoryRow } from '../rows/StoryRow';
import type { TaskboardGrouping } from '../taskboardApi';
import type { TaskboardGridView } from '../taskboardGridView';
import type { TaskboardScope } from '../taskboardScope';
import { taskboardTestIds as ids } from '../testIds';
import { passesFaces, UNASSIGNED_FACE } from './faceFilter';
import { gridColumns } from './gridColumns';

/** The column headers and the rows of one grouping. It sits inside a TaskboardDnd. */
export function TaskboardGrid({
  projectKey,
  projectId,
  scope,
  view,
  rows,
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
  view: TaskboardGridView;
  /** The story rows this grid draws: all of `view.rows`, or a subset (a section of the Feature board). The column counts are theirs. */
  rows: TaskboardRowDto[];
  grouping: TaskboardGrouping;
  faces: ReadonlySet<string>;
  me: UserRef | undefined;
  isCollapsed: (rowId: string, completed: boolean) => boolean;
  onToggle: (rowId: string, completed: boolean) => void;
  onSetState: (parentId: string, key: string, revision: number, stateId: string) => void;
}) {
  // The face filter works on the loaded view: it is the same GET's data and the endpoint has no assignee filter.
  const shownIn = (stateId: string) =>
    (grouping === 'stories' ? rows : view.peopleRows)
      .flatMap((row) => row.cells)
      .filter((cell) => cell.stateId === stateId)
      .flatMap((cell) => cell.tasks)
      .filter((card) => passesFaces(card, faces)).length;
  return (
    <>
      <div data-testid={ids.grid} role="grid" aria-label="Taskboard" className="overflow-x-auto">
        <div
          className="sticky top-0 z-10 grid bg-panel text-[length:var(--text-sm)]"
          style={{ gridTemplateColumns: gridColumns(view.columns.length) }}
        >
          <div className="px-2 py-2 text-[length:var(--text-xs)] text-ink-muted">
            {grouping === 'stories' ? 'Story / bug' : 'Person'}
          </div>
          {view.columns.map((column) => (
            <div
              key={column.state.id}
              data-testid={ids.columnHeader}
              className="flex gap-2 px-2 py-2"
            >
              <StateDot category={column.state.category} name={column.state.name} />
              <span data-testid={ids.columnCount} className="text-ink-muted">
                {shownIn(column.state.id)}
              </span>
            </div>
          ))}
        </div>
        {grouping === 'stories'
          ? rows.map((row) => {
              const completed = row.parent?.state.category === 'completed';
              const id = row.parent?.id ?? 'unparented';
              return (
                <StoryRow
                  key={id}
                  projectKey={projectKey}
                  projectId={projectId}
                  scope={scope}
                  row={row}
                  columns={view.columns}
                  parentStates={view.parentStates}
                  view={view}
                  faces={faces}
                  collapsed={isCollapsed(id, completed)}
                  onToggle={() => onToggle(id, completed)}
                  onSetState={(stateId) =>
                    row.parent &&
                    onSetState(row.parent.id, row.parent.key, row.parent.revision, stateId)
                  }
                />
              );
            })
          : view.peopleRows
              .filter((row) => faces.size === 0 || faces.has(row.person?.id ?? UNASSIGNED_FACE))
              .map((row) => (
                <PersonRow
                  key={row.person?.id ?? 'unassigned'}
                  row={row}
                  columns={view.columns}
                  view={view}
                  faces={faces}
                  meId={me?.id}
                />
              ))}
      </div>
    </>
  );
}
