import type { StateRef } from '../../../api/refs';
import type { TaskboardPersonRowDto, TaskboardViewDto } from '../../../api/views';
import { Avatar } from '../../../components/Avatar';
import { passesFaces } from '../grid/faceFilter';
import { gridColumns } from '../grid/gridColumns';
import { taskboardTestIds as ids } from '../testIds';
import { TaskCell } from './TaskCell';

/** A People row (`row.person` null = Unassigned): the header shows counts only, then one cell per Task state. */
export function PersonRow({
  row,
  columns,
  view,
  faces,
  meId,
}: {
  row: TaskboardPersonRowDto;
  columns: { state: StateRef }[];
  view: Pick<TaskboardViewDto, 'styleRules' | 'cardFields'>;
  faces: ReadonlySet<string>;
  meId: string | undefined;
}) {
  const rowId = row.person?.id ?? 'unassigned';
  const label = row.taskCount === 1 ? 'task' : 'tasks';
  return (
    <div
      data-testid={ids.row}
      data-row={rowId}
      role="row"
      className="grid border-t border-edge"
      style={{ gridTemplateColumns: gridColumns(columns.length) }}
    >
      <div data-testid={ids.rowHeader} className="grid content-start gap-1 px-2 py-2">
        <span className="flex items-center gap-2 text-[length:var(--text-sm)] text-ink">
          <Avatar {...(row.person ?? {})} />
          <span data-testid={ids.rowTitle}>{row.person?.name ?? 'Unassigned'}</span>
          {row.person && row.person.id === meId ? (
            <span className="text-ink-muted">(you)</span>
          ) : null}
        </span>
        <span data-testid={ids.rowCount} className="text-[length:var(--text-xs)] text-ink-muted">
          {row.taskCount} {label}
        </span>
      </div>
      {columns.map((column) => (
        <TaskCell
          key={column.state.id}
          rowId={rowId}
          stateId={column.state.id}
          cards={(row.cells.find((c) => c.stateId === column.state.id)?.tasks ?? []).filter(
            (card) => passesFaces(card, faces),
          )}
          view={view}
          showParent
        />
      ))}
    </div>
  );
}
