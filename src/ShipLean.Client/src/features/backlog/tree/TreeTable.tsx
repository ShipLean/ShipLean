import {
  closestCenter,
  DndContext,
  type CollisionDetection,
  KeyboardSensor,
  pointerWithin,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import { useState } from 'react';
import type { BacklogRowDto } from '../../../api/views';
import { BACKLOG_COLUMNS, type BacklogColumnId } from '../backlogApi';
import { NewItemRow } from '../list/NewItemRow';
import { backlogTestIds as ids } from '../testIds';
import { TreeGroupRow } from './TreeGroupRow';
import { TreeItemRow } from './TreeItemRow';
import { visibleRows } from './treeRows';

// The # and Parent columns say nothing in a tree (the nesting is the parent), so the table leaves them out.
const HIDDEN_IN_TREE: readonly BacklogColumnId[] = ['rank', 'parent'];
const widths: Partial<Record<BacklogColumnId, string>> = {
  key: '90px',
  state: '120px',
  points: '64px',
  sprint: '110px',
  assignee: '72px',
};

// A pointer drag lands on the row under the pointer; a keyboard drag has no pointer, so the nearest row decides.
const collide: CollisionDetection = (args) =>
  args.pointerCoordinates === null ? closestCenter(args) : pointerWithin(args);

// The tree table: Epic and Feature groups with their stories, then No parent. Groups fold; a story is dragged by its
// grip onto a group or another story.
export function TreeTable({
  rows,
  columns,
  projectId,
  projectKey,
  adding,
  onCloseAdd,
  dragDisabled,
  filtered,
  includeCompleted,
  flashing,
  onDrop,
}: {
  rows: BacklogRowDto[];
  columns: readonly BacklogColumnId[];
  projectId: string;
  projectKey: string;
  adding: boolean;
  onCloseAdd: () => void;
  dragDisabled: boolean;
  filtered: boolean;
  includeCompleted: boolean;
  flashing: ReadonlySet<string>;
  onDrop: (active: BacklogRowDto, over: BacklogRowDto) => void;
}) {
  const [collapsed, setCollapsed] = useState<ReadonlySet<string>>(new Set());
  const shownColumns = columns.filter((c) => !HIDDEN_IN_TREE.includes(c));
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor),
  );
  function toggle(id: string) {
    const next = new Set(collapsed);
    if (!next.delete(id)) next.add(id);
    setCollapsed(next);
  }
  function onDragEnd({ active, over }: DragEndEvent) {
    const from = active.data.current?.row as BacklogRowDto | undefined;
    const to = over?.data.current?.row as BacklogRowDto | undefined;
    if (from && to) onDrop(from, to);
  }
  const shown = visibleRows(rows, collapsed);
  return (
    <div data-testid={ids.list} className="flex-1 overflow-auto">
      <DndContext sensors={sensors} collisionDetection={collide} onDragEnd={onDragEnd}>
        <table className="w-full border-collapse">
          <thead>
            <tr className="h-8 border-b border-edge text-left text-[length:var(--text-xs)] text-ink-muted">
              <th className="w-6" />
              {shownColumns.map((column) => (
                <th
                  key={column}
                  data-testid={ids.columnHeader}
                  data-column={column}
                  className={`px-2 font-semibold ${column === 'points' ? 'text-right' : ''}`}
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
                columnCount={shownColumns.length + 1}
                onClose={onCloseAdd}
              />
            ) : null}
            {shown.map((row) =>
              row.kind === 'item' ? (
                <TreeItemRow
                  key={row.id}
                  row={row}
                  columns={shownColumns}
                  projectKey={projectKey}
                  dragDisabled={dragDisabled}
                  flash={flashing.has(row.id)}
                />
              ) : (
                <TreeGroupRow
                  key={row.id}
                  row={row}
                  columnCount={shownColumns.length + 1}
                  open={!collapsed.has(row.id)}
                  includeCompleted={includeCompleted}
                  onToggle={() => toggle(row.id)}
                />
              ),
            )}
            {rows.length === 0 ? (
              <tr>
                <td
                  colSpan={shownColumns.length + 1}
                  data-testid={ids.noRows}
                  className="h-16 text-center text-[length:var(--text-sm)] text-ink-muted"
                >
                  {filtered ? 'Nothing matches this filter' : 'Nothing to show'}
                </td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </DndContext>
    </div>
  );
}
