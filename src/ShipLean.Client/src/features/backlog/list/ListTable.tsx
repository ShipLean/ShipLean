import {
  closestCenter,
  DndContext,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import type { BacklogRowDto } from '../../../api/views';
import { BACKLOG_COLUMNS, type BacklogColumnId } from '../backlogApi';
import { backlogTestIds as ids } from '../testIds';
import { reorderTarget, type ReorderMove } from '../drag/reorderTarget';
import { ListRow } from './ListRow';
import { NewItemRow } from './NewItemRow';

const widths: Partial<Record<BacklogColumnId, string>> = {
  rank: '48px',
  key: '90px',
  parent: '170px',
  state: '120px',
  points: '64px',
  sprint: '110px',
  assignee: '72px',
};
const numeric: readonly BacklogColumnId[] = ['rank', 'points'];

// The ranked table: a header, the optional inline new-item row, then one row per item.
export function ListTable({
  rows,
  columns,
  projectId,
  projectKey,
  adding,
  onCloseAdd,
  dragDisabled,
  filtered,
  flashing,
  onReorder,
}: {
  rows: BacklogRowDto[];
  columns: readonly BacklogColumnId[];
  projectId: string;
  projectKey: string;
  adding: boolean;
  onCloseAdd: () => void;
  // Reordering is off (a filter is on).
  dragDisabled: boolean;
  // A filter is on: an empty list means nothing matches it.
  filtered: boolean;
  // Ids of the rows a teammate just changed.
  flashing: ReadonlySet<string>;
  onReorder: (row: BacklogRowDto, move: ReorderMove) => void;
}) {
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );
  const rowIds = rows.map((row) => row.id);
  function onDragEnd({ active, over }: DragEndEvent) {
    if (over === null) return;
    const move = reorderTarget(rowIds, String(active.id), String(over.id));
    const row = rows.find((r) => r.id === active.id);
    if (move && row) onReorder(row, move);
  }
  return (
    <div data-testid={ids.list} className="flex-1 overflow-auto">
      <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
        <SortableContext items={rowIds} strategy={verticalListSortingStrategy}>
          <table className="w-full border-collapse">
            <thead>
              <tr className="h-8 border-b border-edge text-left text-[length:var(--text-xs)] text-ink-muted">
                <th className="w-6" />
                {columns.map((column) => (
                  <th
                    key={column}
                    data-testid={ids.columnHeader}
                    data-column={column}
                    className={`px-2 font-semibold ${numeric.includes(column) ? 'text-right' : ''}`}
                    style={{ width: widths[column] }}
                  >
                    {BACKLOG_COLUMNS.find((c) => c.id === column)?.label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {adding ? (
                <NewItemRow
                  projectId={projectId}
                  projectKey={projectKey}
                  columnCount={columns.length + 1}
                  onClose={onCloseAdd}
                />
              ) : null}
              {rows.map((row) => (
                <ListRow
                  key={row.id}
                  row={row}
                  columns={columns}
                  projectKey={projectKey}
                  dragDisabled={dragDisabled}
                  flash={flashing.has(row.id)}
                />
              ))}
              {rows.length === 0 ? (
                <tr>
                  <td
                    colSpan={columns.length + 1}
                    data-testid={ids.noRows}
                    className="h-16 text-center text-[length:var(--text-sm)] text-ink-muted"
                  >
                    {filtered ? 'Nothing matches this filter' : 'Nothing to show'}
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </SortableContext>
      </DndContext>
    </div>
  );
}
